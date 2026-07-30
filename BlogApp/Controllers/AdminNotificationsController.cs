using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services;
using BlogApp.Services.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace BlogApp.Controllers;

[Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
public class AdminNotificationsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationDispatcher _dispatcher;

    public AdminNotificationsController(ApplicationDbContext db, INotificationDispatcher dispatcher)
    {
        _db = db;
        _dispatcher = dispatcher;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Send notifications";
        var isSuper = AuthorAccess.IsSuperAdmin(User);
        var userId = AuthorAccess.UserId(User)!;

        var q = _db.NotificationCampaigns.AsNoTracking().AsQueryable();
        if (!isSuper)
            q = q.Where(c => c.CreatedByUserId == userId);

        var recent = await q.OrderByDescending(c => c.CreatedAtUtc).Take(40).ToListAsync();
        ViewBag.Recent = recent;
        ViewBag.IsSuperAdmin = isSuper;
        ViewBag.Categories = await _db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        return View(new ComposeNotificationVm());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Compose(ComposeNotificationVm model)
    {
        ViewData["Title"] = "Send notifications";
        var isSuper = AuthorAccess.IsSuperAdmin(User);
        var userId = AuthorAccess.UserId(User)!;
        ViewBag.IsSuperAdmin = isSuper;
        ViewBag.Categories = await _db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync();

        // Authors may only notify their own followers
        if (!isSuper)
        {
            model.Audience = NotificationAudience.AuthorFollowers;
            model.AuthorUserId = userId;
            model.Kind = NotificationKind.NewPost;
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Recent = await LoadRecentAsync(isSuper, userId);
            return View("Index", model);
        }

        var campaign = new NotificationCampaign
        {
            Title = model.Title.Trim(),
            Body = string.IsNullOrWhiteSpace(model.Body) ? null : model.Body.Trim(),
            LinkUrl = string.IsNullOrWhiteSpace(model.LinkUrl) ? null : model.LinkUrl.Trim(),
            Kind = model.Kind,
            Audience = model.Audience,
            TargetUserId = model.TargetUserId,
            AuthorUserId = model.Audience == NotificationAudience.AuthorFollowers
                ? (model.AuthorUserId ?? userId)
                : model.AuthorUserId,
            CategoryId = model.CategoryId,
            TargetUserIdsCsv = model.TargetUserIdsCsv,
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
            ScheduledAtUtc = model.Schedule
                ? model.ScheduledAtUtc?.ToUniversalTime()
                : null
        };

        // Safety: non-super cannot broadcast / all-authors / category
        if (!isSuper && campaign.Audience is not NotificationAudience.AuthorFollowers
            and not NotificationAudience.SingleUser)
        {
            TempData["Error"] = "Authors can only notify their followers or a single user.";
            ViewBag.Recent = await LoadRecentAsync(isSuper, userId);
            return View("Index", model);
        }

        _db.NotificationCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        if (campaign.ScheduledAtUtc is null || campaign.ScheduledAtUtc <= DateTime.UtcNow)
        {
            var n = await _dispatcher.DispatchCampaignAsync(campaign);
            TempData["Saved"] = $"Sent to {n} recipient(s).";
        }
        else
        {
            TempData["Saved"] = $"Scheduled for {campaign.ScheduledAtUtc:u} UTC.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<List<NotificationCampaign>> LoadRecentAsync(bool isSuper, string userId)
    {
        var q = _db.NotificationCampaigns.AsNoTracking().AsQueryable();
        if (!isSuper) q = q.Where(c => c.CreatedByUserId == userId);
        return await q.OrderByDescending(c => c.CreatedAtUtc).Take(40).ToListAsync();
    }
}

public class ComposeNotificationVm
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Body { get; set; }

    [MaxLength(400)]
    public string? LinkUrl { get; set; }

    public NotificationKind Kind { get; set; } = NotificationKind.AdminMessage;

    public NotificationAudience Audience { get; set; } = NotificationAudience.AllAuthors;

    [MaxLength(450)]
    public string? TargetUserId { get; set; }

    [MaxLength(450)]
    public string? AuthorUserId { get; set; }

    public int? CategoryId { get; set; }

    [MaxLength(4000)]
    public string? TargetUserIdsCsv { get; set; }

    public bool Schedule { get; set; }

    public DateTime? ScheduledAtUtc { get; set; }
}
