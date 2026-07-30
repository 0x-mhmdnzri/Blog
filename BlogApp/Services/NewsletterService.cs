using System.Security.Cryptography;
using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services.Messaging;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Services;

public interface INewsletterService
{
    Task<(bool Ok, string MessageKey)> SubscribeAsync(string email, string? name, string languageCode, string? tags, string? source, string baseUrl, CancellationToken ct = default);
    Task<(bool Ok, string MessageKey)> ConfirmAsync(string token, CancellationToken ct = default);
    Task<(bool Ok, string MessageKey)> UnsubscribeAsync(string token, CancellationToken ct = default);
    Task<List<NewsletterSubscriber>> ResolveAudienceAsync(int? segmentId, string? languageFilter, string? tagFilter, CancellationToken ct = default);
    Task SendCampaignAsync(int campaignId, CancellationToken ct = default);
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
        email = (email ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(email) || !email.Contains('@') || email.Length > 200)
            return (false, "nl.err_email");

        var lang = AppCultures.Normalize(languageCode);
        var existing = await _db.NewsletterSubscribers.FirstOrDefaultAsync(s => s.Email == email, ct);

        if (existing is not null)
        {
            if (existing.Status == NewsletterSubscriberStatus.Confirmed)
                return (true, "nl.already_subscribed");

            if (existing.Status == NewsletterSubscriberStatus.Unsubscribed)
            {
                existing.Status = NewsletterSubscriberStatus.Pending;
                existing.ConfirmToken = NewToken();
                existing.UnsubscribeToken = NewToken();
                existing.SubscribedAtUtc = DateTime.UtcNow;
                existing.UnsubscribedAtUtc = null;
                existing.Name = name?.Trim() ?? existing.Name;
                existing.LanguageCode = lang;
                existing.SegmentTags = tags?.Trim();
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
                    // allow pending too — rare
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
                var unsub = $"<p style=\"font-size:12px;color:#888\"><a href=\"{{{{unsub}}}}\">Unsubscribe</a></p>";
                // Controller will pass absolute unsub when building; use token path relative
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
}
