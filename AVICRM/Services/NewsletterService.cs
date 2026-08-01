using System.Security.Cryptography;
using System.Text;
using AVICRM.Data;
using AVICRM.Models;
using AVICRM.Services.Messaging;
using Microsoft.EntityFrameworkCore;

namespace AVICRM.Services;

public interface INewsletterService
{
    Task<(bool Ok, string MessageKey)> SubscribeAsync(string email, string? name, string languageCode, string? tags, string? source, string baseUrl, CancellationToken ct = default);
    Task<(bool Ok, string MessageKey)> ConfirmAsync(string token, CancellationToken ct = default);
    Task<(bool Ok, string MessageKey)> UnsubscribeAsync(string token, CancellationToken ct = default);
    Task<List<NewsletterSubscriber>> ResolveAudienceAsync(int? segmentId, string? languageFilter, string? tagFilter, CancellationToken ct = default);
    Task SendCampaignAsync(int campaignId, CancellationToken ct = default);

    /// <summary>
    /// Import subscribers from CSV. Always double opt-in: new/re-subscribed rows become Pending
    /// and receive a confirmation email. Confirmed emails are skipped (not re-mailed).
    /// CSV columns: email[,name][,language][,tags]
    /// </summary>
    Task<NewsletterImportResult> ImportCsvAsync(Stream csvStream, string baseUrl, string? defaultLanguage, string? defaultTags, CancellationToken ct = default);

    /// <summary>One-action: create campaign from a published post and send (or schedule).</summary>
    Task<(bool Ok, string Message, int CampaignId)> PublishPostAsCampaignAsync(int postId, string userId, string siteBaseUrl, bool sendNow = true, CancellationToken ct = default);
}

public sealed class NewsletterImportResult
{
    public int Added { get; set; }
    public int Reopened { get; set; }
    public int SkippedConfirmed { get; set; }
    public int SkippedInvalid { get; set; }
    public int ConfirmEmailsQueued { get; set; }
    public List<string> Errors { get; set; } = new();
}

public sealed class NewsletterService : INewsletterService
{
    private readonly ApplicationDbContext _db;
    private readonly IEmailSender _email;
    private readonly IUiTranslator _t;
    private readonly ILogger<NewsletterService> _logger;

    public NewsletterService(
        ApplicationDbContext db,
        IEmailSender email,
        IUiTranslator t,
        ILogger<NewsletterService> logger)
    {
        _db = db;
        _email = email;
        _t = t;
        _logger = logger;
    }

    public async Task<(bool Ok, string MessageKey)> SubscribeAsync(
        string email, string? name, string languageCode, string? tags, string? source, string baseUrl, CancellationToken ct = default)
    {
        email = NormalizeEmail(email);
        if (string.IsNullOrEmpty(email) || !IsValidEmail(email))
            return (false, "nl.err_email");

        var lang = AppCultures.Normalize(languageCode);
        var existing = await _db.NewsletterSubscribers.FirstOrDefaultAsync(s => s.Email == email, ct);

        if (existing is not null)
        {
            if (existing.Status == NewsletterSubscriberStatus.Confirmed)
                return (true, "nl.already_subscribed");

            if (existing.Status == NewsletterSubscriberStatus.Unsubscribed
                || existing.Status == NewsletterSubscriberStatus.Bounced)
            {
                existing.Status = NewsletterSubscriberStatus.Pending;
                existing.ConfirmToken = NewToken();
                existing.UnsubscribeToken = NewToken();
                existing.SubscribedAtUtc = DateTime.UtcNow;
                existing.UnsubscribedAtUtc = null;
                existing.ConfirmedAtUtc = null;
                existing.Name = name?.Trim() ?? existing.Name;
                existing.LanguageCode = lang;
                existing.SegmentTags = tags?.Trim() ?? existing.SegmentTags;
                existing.Source = source ?? existing.Source;
                await _db.SaveChangesAsync(ct);
                await SendConfirmEmailAsync(existing, baseUrl, ct);
                return (true, "nl.check_email");
            }

            // Pending — resend confirm
            existing.ConfirmToken = NewToken();
            existing.Name = name?.Trim() ?? existing.Name;
            await _db.SaveChangesAsync(ct);
            await SendConfirmEmailAsync(existing, baseUrl, ct);
            return (true, "nl.check_email");
        }

        var sub = new NewsletterSubscriber
        {
            Email = email,
            Name = name?.Trim(),
            LanguageCode = lang,
            SegmentTags = tags?.Trim(),
            Status = NewsletterSubscriberStatus.Pending,
            ConfirmToken = NewToken(),
            UnsubscribeToken = NewToken(),
            SubscribedAtUtc = DateTime.UtcNow,
            Source = source ?? "web"
        };
        _db.NewsletterSubscribers.Add(sub);
        await _db.SaveChangesAsync(ct);
        await SendConfirmEmailAsync(sub, baseUrl, ct);
        return (true, "nl.check_email");
    }

    public async Task<(bool Ok, string MessageKey)> ConfirmAsync(string token, CancellationToken ct = default)
    {
        token = (token ?? "").Trim();
        if (string.IsNullOrEmpty(token)) return (false, "nl.err_token");

        var sub = await _db.NewsletterSubscribers.FirstOrDefaultAsync(s => s.ConfirmToken == token, ct);
        if (sub is null) return (false, "nl.err_token");

        if (sub.Status == NewsletterSubscriberStatus.Confirmed)
            return (true, "nl.already_confirmed");

        sub.Status = NewsletterSubscriberStatus.Confirmed;
        sub.ConfirmedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return (true, "nl.confirmed");
    }

    public async Task<(bool Ok, string MessageKey)> UnsubscribeAsync(string token, CancellationToken ct = default)
    {
        token = (token ?? "").Trim();
        if (string.IsNullOrEmpty(token)) return (false, "nl.err_token");

        var sub = await _db.NewsletterSubscribers.FirstOrDefaultAsync(s => s.UnsubscribeToken == token, ct);
        if (sub is null) return (false, "nl.err_token");

        sub.Status = NewsletterSubscriberStatus.Unsubscribed;
        sub.UnsubscribedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return (true, "nl.unsubscribed");
    }

    public async Task<List<NewsletterSubscriber>> ResolveAudienceAsync(
        int? segmentId, string? languageFilter, string? tagFilter, CancellationToken ct = default)
    {
        var q = _db.NewsletterSubscribers.AsNoTracking()
            .Where(s => s.Status == NewsletterSubscriberStatus.Confirmed);

        if (segmentId is int sid)
        {
            var seg = await _db.NewsletterSegments.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sid, ct);
            if (seg is not null)
            {
                if (!string.IsNullOrWhiteSpace(seg.LanguageCode))
                    q = q.Where(s => s.LanguageCode == seg.LanguageCode);
                if (!string.IsNullOrWhiteSpace(seg.RequiredTag))
                {
                    var tag = seg.RequiredTag;
                    q = q.Where(s => s.SegmentTags != null && s.SegmentTags.Contains(tag));
                }
                if (!seg.ConfirmedOnly)
                {
                    q = _db.NewsletterSubscribers.AsNoTracking()
                        .Where(s => s.Status != NewsletterSubscriberStatus.Unsubscribed);
                    if (!string.IsNullOrWhiteSpace(seg.LanguageCode))
                        q = q.Where(s => s.LanguageCode == seg.LanguageCode);
                    if (!string.IsNullOrWhiteSpace(seg.RequiredTag))
                    {
                        var tag = seg.RequiredTag;
                        q = q.Where(s => s.SegmentTags != null && s.SegmentTags.Contains(tag));
                    }
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(languageFilter))
            q = q.Where(s => s.LanguageCode == languageFilter);

        if (!string.IsNullOrWhiteSpace(tagFilter))
        {
            var tag = tagFilter.Trim();
            q = q.Where(s => s.SegmentTags != null && s.SegmentTags.Contains(tag));
        }

        return await q.OrderBy(s => s.Email).ToListAsync(ct);
    }

    public async Task SendCampaignAsync(int campaignId, CancellationToken ct = default)
    {
        var campaign = await _db.NewsletterCampaigns.FirstOrDefaultAsync(c => c.Id == campaignId, ct);
        if (campaign is null) return;
        if (campaign.Status is NewsletterCampaignStatus.Sent or NewsletterCampaignStatus.Cancelled)
            return;

        campaign.Status = NewsletterCampaignStatus.Sending;
        await _db.SaveChangesAsync(ct);

        var audience = await ResolveAudienceAsync(campaign.SegmentId, campaign.LanguageFilter, campaign.TagFilter, ct);
        campaign.RecipientCount = audience.Count;

        var sent = 0;
        var fail = 0;
        foreach (var sub in audience)
        {
            try
            {
                var unsub = "<p style=\"font-size:12px;color:#888\"><a href=\"{{unsub}}\">Unsubscribe</a></p>";
                var body = campaign.BodyHtml + unsub.Replace("{{unsub}}", "/Newsletter/Unsubscribe?token=" + Uri.EscapeDataString(sub.UnsubscribeToken));
                await _email.SendAsync(sub.Email, campaign.Subject, body, true, ct);
                sent++;
            }
            catch (Exception ex)
            {
                fail++;
                _logger.LogWarning(ex, "Newsletter send failed Campaign={Id} Email={Email}", campaignId, sub.Email);
            }
        }

        campaign.SentCount = sent;
        campaign.FailCount = fail;
        campaign.Status = NewsletterCampaignStatus.Sent;
        campaign.SentAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Newsletter campaign {Id} sent={Sent} fail={Fail}", campaignId, sent, fail);
    }

    public async Task<NewsletterImportResult> ImportCsvAsync(
        Stream csvStream, string baseUrl, string? defaultLanguage, string? defaultTags, CancellationToken ct = default)
    {
        var result = new NewsletterImportResult();
        var langDefault = AppCultures.Normalize(defaultLanguage ?? AppCultures.Default);
        var tagsDefault = string.IsNullOrWhiteSpace(defaultTags) ? null : defaultTags.Trim();

        using var reader = new StreamReader(csvStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var lineNo = 0;
        var pendingConfirm = new List<NewsletterSubscriber>();

        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct);
            lineNo++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Strip BOM / ignore header
            if (lineNo == 1 && IsHeaderRow(line))
                continue;

            var cols = ParseCsvLine(line);
            if (cols.Count == 0) continue;

            var email = NormalizeEmail(cols[0]);
            if (!IsValidEmail(email))
            {
                result.SkippedInvalid++;
                if (result.Errors.Count < 20)
                    result.Errors.Add($"L{lineNo}: invalid email");
                continue;
            }

            var name = cols.Count > 1 ? NullIfEmpty(cols[1]) : null;
            var lang = cols.Count > 2 && !string.IsNullOrWhiteSpace(cols[2])
                ? AppCultures.Normalize(cols[2])
                : langDefault;
            var tags = cols.Count > 3 && !string.IsNullOrWhiteSpace(cols[3])
                ? cols[3].Trim()
                : tagsDefault;

            var existing = await _db.NewsletterSubscribers.FirstOrDefaultAsync(s => s.Email == email, ct);
            if (existing is not null)
            {
                if (existing.Status == NewsletterSubscriberStatus.Confirmed)
                {
                    result.SkippedConfirmed++;
                    continue;
                }

                // Pending / Unsubscribed / Bounced → re-open as Pending (double opt-in)
                existing.Status = NewsletterSubscriberStatus.Pending;
                existing.ConfirmToken = NewToken();
                if (string.IsNullOrEmpty(existing.UnsubscribeToken))
                    existing.UnsubscribeToken = NewToken();
                existing.SubscribedAtUtc = DateTime.UtcNow;
                existing.UnsubscribedAtUtc = null;
                existing.ConfirmedAtUtc = null;
                if (name is not null) existing.Name = name;
                existing.LanguageCode = lang;
                if (tags is not null) existing.SegmentTags = tags;
                existing.Source = "csv-import";
                result.Reopened++;
                pendingConfirm.Add(existing);
            }
            else
            {
                var sub = new NewsletterSubscriber
                {
                    Email = email,
                    Name = name,
                    LanguageCode = lang,
                    SegmentTags = tags,
                    Status = NewsletterSubscriberStatus.Pending,
                    ConfirmToken = NewToken(),
                    UnsubscribeToken = NewToken(),
                    SubscribedAtUtc = DateTime.UtcNow,
                    Source = "csv-import"
                };
                _db.NewsletterSubscribers.Add(sub);
                result.Added++;
                pendingConfirm.Add(sub);
            }

            // Cap per request to avoid abuse
            if (result.Added + result.Reopened >= 2000)
            {
                result.Errors.Add("Import capped at 2000 rows this request.");
                break;
            }
        }

        await _db.SaveChangesAsync(ct);

        // Double opt-in: always send confirm (never auto-confirm from CSV)
        foreach (var sub in pendingConfirm)
        {
            try
            {
                await SendConfirmEmailAsync(sub, baseUrl, ct);
                result.ConfirmEmailsQueued++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CSV import confirm email failed {Email}", sub.Email);
            }
        }

        return result;
    }

    public async Task<(bool Ok, string Message, int CampaignId)> PublishPostAsCampaignAsync(
        int postId, string userId, string siteBaseUrl, bool sendNow = true, CancellationToken ct = default)
    {
        var post = await _db.Posts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == postId, ct);
        if (post is null)
            return (false, "Post not found.", 0);
        if (!post.IsPublished)
            return (false, "Publish the post before sending to the newsletter.", 0);

        var title = System.Net.WebUtility.HtmlEncode(post.Title);
        var summary = System.Net.WebUtility.HtmlEncode(post.Summary ?? "");
        var slug = Uri.EscapeDataString(post.Slug);
        var url = $"{siteBaseUrl.TrimEnd('/')}/post/{post.Slug}";

        var bodyHtml =
            $"<h2>{title}</h2>\n" +
            (string.IsNullOrWhiteSpace(summary) ? "" : $"<p>{summary}</p>\n") +
            $"<p><a href=\"{url}\">Read full post →</a></p>";

        var campaign = new NewsletterCampaign
        {
            Subject = post.Title.Length > 200 ? post.Title[..197] + "…" : post.Title,
            BodyHtml = bodyHtml,
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
            Status = sendNow ? NewsletterCampaignStatus.Scheduled : NewsletterCampaignStatus.Draft,
            ScheduledAtUtc = sendNow ? DateTime.UtcNow : null
        };
        _db.NewsletterCampaigns.Add(campaign);
        await _db.SaveChangesAsync(ct);

        if (sendNow)
        {
            try
            {
                await SendCampaignAsync(campaign.Id, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PublishPostAsCampaign send failed PostId={Id} Campaign={Cid}", postId, campaign.Id);
                return (true, "Campaign created but send failed — check Admin → Newsletter.", campaign.Id);
            }
        }

        return (true, "Newsletter campaign created from post.", campaign.Id);
    }

    private async Task SendConfirmEmailAsync(NewsletterSubscriber sub, string baseUrl, CancellationToken ct)
    {
        var link = $"{baseUrl.TrimEnd('/')}/Newsletter/Confirm?token={Uri.EscapeDataString(sub.ConfirmToken)}";
        var subject = _t["nl.confirm_subject"];
        var body = $"<p>{_t["nl.confirm_body"]}</p><p><a href=\"{link}\">{link}</a></p>";
        try
        {
            await _email.SendAsync(sub.Email, subject, body, true, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Confirm email failed Email={Email}", sub.Email);
        }
    }

    private static string NewToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string NormalizeEmail(string? email)
        => (email ?? "").Trim().ToLowerInvariant();

    private static bool IsValidEmail(string email)
        => !string.IsNullOrEmpty(email) && email.Contains('@') && email.Length <= 200
           && email.IndexOf('@') > 0 && email.IndexOf('@') < email.Length - 1;

    private static string? NullIfEmpty(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static bool IsHeaderRow(string line)
    {
        var first = ParseCsvLine(line).FirstOrDefault()?.Trim().ToLowerInvariant() ?? "";
        return first is "email" or "e-mail" or "mail";
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',' || c == ';')
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
                else sb.Append(c);
            }
        }
        result.Add(sb.ToString());
        return result;
    }
}
