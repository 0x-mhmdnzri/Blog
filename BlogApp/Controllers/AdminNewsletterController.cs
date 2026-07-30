using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

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
            .OrderByDescending(s => s.SubscribedAtUtc).Take(100).ToListAsync();
        ViewBag.Segments = await _db.NewsletterSegments.OrderBy(s => s.Name).ToListAsync();
        ViewBag.Campaigns = await _db.NewsletterCampaigns
            .Include(c => c.Segment)
            .OrderByDescending(c => c.CreatedAtUtc).Take(50).ToListAsync();

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
}
