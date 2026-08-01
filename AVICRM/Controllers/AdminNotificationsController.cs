using AVICRM.Data;
using AVICRM.Models;
using AVICRM.Services;
using AVICRM.Services.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace AVICRM.Controllers;

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

        ViewBag.Recent = await q.OrderByDescending(c => c.CreatedAtUtc).Take(40).ToListAsync();
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

        if (!isSuper)
        {
            model.Audience = NotificationAudience.AuthorFollowers;
            model.AuthorUserId = userId;
            model.Kind = NotificationKind.NewPost;
        }

        ValidateCompose(model, isSuper);

        if (!ModelState.IsValid)
        {
            ViewBag.Recent = await LoadRecentAsync(isSuper, userId);
            return View("Index", model);
        }

        if (!isSuper && model.Audience is not NotificationAudience.AuthorFollowers
            and not NotificationAudience.SingleUser)
        {
            TempData["Error"] = "Authors can only notify their followers or a single user.";
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
            TargetUserId = string.IsNullOrWhiteSpace(model.TargetUserId) ? null : model.TargetUserId.Trim(),
            AuthorUserId = model.Audience == NotificationAudience.AuthorFollowers
                ? (string.IsNullOrWhiteSpace(model.AuthorUserId) ? userId : model.AuthorUserId.Trim())
                : (string.IsNullOrWhiteSpace(model.AuthorUserId) ? null : model.AuthorUserId.Trim()),
            CategoryId = model.CategoryId,
            TargetUserIdsCsv = string.IsNullOrWhiteSpace(model.TargetUserIdsCsv) ? null : model.TargetUserIdsCsv.Trim(),
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
            ScheduledAtUtc = model.Schedule
                ? model.ScheduledAtUtc?.ToUniversalTime()
                : null
        };

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

    private void ValidateCompose(ComposeNotificationVm model, bool isSuper)
    {
        if (string.IsNullOrWhiteSpace(model.Title))
            ModelState.AddModelError(nameof(model.Title), "Title is required.");

        if (!string.IsNullOrWhiteSpace(model.LinkUrl))
        {
            var link = model.LinkUrl.Trim();
            if (!(link.StartsWith('/') || link.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                  || link.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError(nameof(model.LinkUrl), "Link must start with / or http(s)://");
            }
        }

        if (isSuper)
        {
            switch (model.Audience)
            {
                case NotificationAudience.CategoryReaders when model.CategoryId is null or <= 0:
                    ModelState.AddModelError(nameof(model.CategoryId), "Category is required for this audience.");
                    break;
                case NotificationAudience.SingleUser when string.IsNullOrWhiteSpace(model.TargetUserId):
                    ModelState.AddModelError(nameof(model.TargetUserId), "Target user id is required.");
                    break;
                case NotificationAudience.UserList when string.IsNullOrWhiteSpace(model.TargetUserIdsCsv):
                    ModelState.AddModelError(nameof(model.TargetUserIdsCsv), "At least one user id is required.");
                    break;
            }
        }

        if (model.Schedule)
        {
            if (model.ScheduledAtUtc is null)
                ModelState.AddModelError(nameof(model.ScheduledAtUtc), "Schedule time is required.");
            else if (model.ScheduledAtUtc.Value.ToUniversalTime() < DateTime.UtcNow.AddMinutes(-1))
                ModelState.AddModelError(nameof(model.ScheduledAtUtc), "Schedule time must be in the future.");
        }
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
    [Required(ErrorMessage = "Title is required"), MaxLength(200)]
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
