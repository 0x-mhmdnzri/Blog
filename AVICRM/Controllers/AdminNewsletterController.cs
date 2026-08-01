using AVICRM.Data;
using AVICRM.Models;
using AVICRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace AVICRM.Controllers;

[Authorize(Roles = AppRoles.SuperAdmin)]
public class AdminNewsletterController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly INewsletterService _nl;
    private readonly IUiTranslator _t;

    public AdminNewsletterController(ApplicationDbContext db, INewsletterService nl, IUiTranslator t)
    {
        _db = db;
        _nl = nl;
        _t = t;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? tab = null)
    {
        ViewData["Title"] = _t["admin.nav.newsletter"];
        ViewBag.ActiveTab = tab ?? "overview";

        ViewBag.Total = await _db.NewsletterSubscribers.CountAsync();
        ViewBag.Confirmed = await _db.NewsletterSubscribers.CountAsync(s => s.Status == NewsletterSubscriberStatus.Confirmed);
        ViewBag.Pending = await _db.NewsletterSubscribers.CountAsync(s => s.Status == NewsletterSubscriberStatus.Pending);
        ViewBag.Unsubscribed = await _db.NewsletterSubscribers.CountAsync(s => s.Status == NewsletterSubscriberStatus.Unsubscribed);
        ViewBag.CampaignCount = await _db.NewsletterCampaigns.CountAsync();

        ViewBag.Subscribers = await _db.NewsletterSubscribers
            .OrderByDescending(s => s.SubscribedAtUtc).Take(200).ToListAsync();
        ViewBag.Segments = await _db.NewsletterSegments.OrderBy(s => s.Name).ToListAsync();
        ViewBag.Campaigns = await _db.NewsletterCampaigns
            .Include(c => c.Segment)
            .OrderByDescending(c => c.CreatedAtUtc).Take(50).ToListAsync();

        // Recent published posts for one-click campaign
        ViewBag.RecentPosts = await _db.Posts.AsNoTracking()
            .Where(p => p.IsPublished && p.DeletedAtUtc == null)
            .OrderByDescending(p => p.PublishedAtUtc ?? p.CreatedAtUtc)
            .Select(p => new { p.Id, p.Title, p.Slug })
            .Take(20)
            .ToListAsync();

        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSegment(int? id, string name, string? description,
        string? languageCode, string? requiredTag, bool confirmedOnly = true)
    {
        name = (name ?? "").Trim();
        if (string.IsNullOrEmpty(name))
        {
            TempData["NlErr"] = _t["nl.err_segment"];
            return RedirectToAction(nameof(Index), new { tab = "segments" });
        }

        NewsletterSegment seg;
        if (id is > 0)
            seg = await _db.NewsletterSegments.FindAsync(id) ?? new NewsletterSegment();
        else
        {
            seg = new NewsletterSegment { CreatedAtUtc = DateTime.UtcNow };
            _db.NewsletterSegments.Add(seg);
        }

        if (seg.Id == 0 && id is > 0) _db.NewsletterSegments.Add(seg);

        seg.Name = name;
        seg.Description = description?.Trim();
        seg.LanguageCode = string.IsNullOrWhiteSpace(languageCode) ? null : languageCode.Trim();
        seg.RequiredTag = string.IsNullOrWhiteSpace(requiredTag) ? null : requiredTag.Trim();
        seg.ConfirmedOnly = confirmedOnly;

        await _db.SaveChangesAsync();
        TempData["NlOk"] = _t["nl.saved_segment"];
        return RedirectToAction(nameof(Index), new { tab = "segments" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSegment(int id)
    {
        var seg = await _db.NewsletterSegments.FindAsync(id);
        if (seg is not null)
        {
            _db.NewsletterSegments.Remove(seg);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index), new { tab = "segments" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCampaign(string subject, string bodyHtml, int? segmentId,
        string? languageFilter, string? tagFilter, DateTime? scheduledAtUtc, bool sendNow = false)
    {
        subject = (subject ?? "").Trim();
        if (string.IsNullOrEmpty(subject) || string.IsNullOrWhiteSpace(bodyHtml))
        {
            TempData["NlErr"] = _t["nl.err_campaign"];
            return RedirectToAction(nameof(Index), new { tab = "campaigns" });
        }

        var campaign = new NewsletterCampaign
        {
            Subject = subject,
            BodyHtml = bodyHtml,
            SegmentId = segmentId is > 0 ? segmentId : null,
            LanguageFilter = string.IsNullOrWhiteSpace(languageFilter) ? null : languageFilter.Trim(),
            TagFilter = string.IsNullOrWhiteSpace(tagFilter) ? null : tagFilter.Trim(),
            CreatedByUserId = AuthorAccess.UserId(User)!,
            CreatedAtUtc = DateTime.UtcNow,
            Status = NewsletterCampaignStatus.Draft
        };

        if (sendNow)
        {
            campaign.Status = NewsletterCampaignStatus.Scheduled;
            campaign.ScheduledAtUtc = DateTime.UtcNow;
        }
        else if (scheduledAtUtc is DateTime when && when > DateTime.UtcNow.AddMinutes(-1))
        {
            campaign.Status = NewsletterCampaignStatus.Scheduled;
            campaign.ScheduledAtUtc = DateTime.SpecifyKind(when, DateTimeKind.Utc);
        }

        _db.NewsletterCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        if (sendNow)
        {
            await _nl.SendCampaignAsync(campaign.Id);
            TempData["NlOk"] = _t["nl.campaign_sent"];
        }
        else
        {
            TempData["NlOk"] = _t["nl.campaign_saved"];
        }

        return RedirectToAction(nameof(Index), new { tab = "campaigns" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SendCampaign(int id)
    {
        await _nl.SendCampaignAsync(id);
        TempData["NlOk"] = _t["nl.campaign_sent"];
        return RedirectToAction(nameof(Index), new { tab = "campaigns" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelCampaign(int id)
    {
        var c = await _db.NewsletterCampaigns.FindAsync(id);
        if (c is not null && c.Status is NewsletterCampaignStatus.Draft or NewsletterCampaignStatus.Scheduled)
        {
            c.Status = NewsletterCampaignStatus.Cancelled;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index), new { tab = "campaigns" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSubscriber(int id)
    {
        var s = await _db.NewsletterSubscribers.FindAsync(id);
        if (s is not null)
        {
            _db.NewsletterSubscribers.Remove(s);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index), new { tab = "subscribers" });
    }

    /// <summary>CSV import — always double opt-in (Pending + confirm email).</summary>
    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(5_000_000)]
    public async Task<IActionResult> ImportCsv(IFormFile? file, string? defaultLanguage, string? defaultTags)
    {
        if (file is null || file.Length == 0)
        {
            TempData["NlErr"] = _t["nl.import_no_file"];
            return RedirectToAction(nameof(Index), new { tab = "subscribers" });
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        await using var stream = file.OpenReadStream();
        var result = await _nl.ImportCsvAsync(stream, baseUrl, defaultLanguage, defaultTags);

        TempData["NlOk"] = string.Format(
            _t["nl.import_result"],
            result.Added,
            result.Reopened,
            result.SkippedConfirmed,
            result.SkippedInvalid,
            result.ConfirmEmailsQueued);

        if (result.Errors.Count > 0)
            TempData["NlErr"] = string.Join(" · ", result.Errors.Take(5));

        return RedirectToAction(nameof(Index), new { tab = "subscribers" });
    }

    [HttpGet]
    public async Task<IActionResult> ExportSubscribersCsv()
    {
        var rows = await _db.NewsletterSubscribers.AsNoTracking()
            .OrderBy(s => s.Email)
            .Select(s => new { s.Email, s.Name, Status = s.Status.ToString(), s.LanguageCode, s.SegmentTags, s.Source, s.SubscribedAtUtc, s.ConfirmedAtUtc })
            .ToListAsync();

        var sb = new StringBuilder();
        sb.Append('\uFEFF'); // BOM for Excel
        sb.AppendLine("email,name,status,language,tags,source,subscribed_at_utc,confirmed_at_utc");
        foreach (var r in rows)
        {
            sb.Append(Csv(r.Email)).Append(',')
              .Append(Csv(r.Name)).Append(',')
              .Append(Csv(r.Status)).Append(',')
              .Append(Csv(r.LanguageCode)).Append(',')
              .Append(Csv(r.SegmentTags)).Append(',')
              .Append(Csv(r.Source)).Append(',')
              .Append(Csv(r.SubscribedAtUtc.ToString("o"))).Append(',')
              .Append(Csv(r.ConfirmedAtUtc?.ToString("o"))).AppendLine();
        }

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv; charset=utf-8", "newsletter-subscribers.csv");
    }

    /// <summary>One-click: publish selected post as campaign (FEATURES.md).</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PublishPostCampaign(int postId, bool sendNow = true)
    {
        var userId = AuthorAccess.UserId(User)!;
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var (ok, msg, campaignId) = await _nl.PublishPostAsCampaignAsync(postId, userId, baseUrl, sendNow);
        TempData[ok ? "NlOk" : "NlErr"] = msg + (campaignId > 0 ? $" (#{campaignId})" : "");
        return RedirectToAction(nameof(Index), new { tab = "campaigns" });
    }

    private static string Csv(string? v)
    {
        if (string.IsNullOrEmpty(v)) return "";
        if (v.Contains(',') || v.Contains('"') || v.Contains('\n') || v.Contains('\r'))
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        return v;
    }
}
