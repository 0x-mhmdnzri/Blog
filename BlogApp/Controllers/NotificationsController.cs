using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

[Authorize(Roles = AppRoles.Reader + "," + AppRoles.Author + "," + AppRoles.SuperAdmin)]
public class NotificationsController : Controller
{
    private readonly ApplicationDbContext _db;

    public NotificationsController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = AuthorAccess.UserId(User)!;
        var items = await _db.AppNotifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(100)
            .ToListAsync();
        ViewData["Title"] = "اعلان‌ها";
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> UnreadCount()
    {
        var userId = AuthorAccess.UserId(User)!;
        var count = await _db.AppNotifications.CountAsync(n => n.UserId == userId && !n.IsRead);
        return Json(new { count });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id)
    {
        var userId = AuthorAccess.UserId(User)!;
        var n = await _db.AppNotifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (n is null) return NotFound();
        n.IsRead = true;
        await _db.SaveChangesAsync();
        if (!string.IsNullOrEmpty(n.LinkUrl) && Url.IsLocalUrl(n.LinkUrl))
            return Redirect(n.LinkUrl);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = AuthorAccess.UserId(User)!;
        await _db.AppNotifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Preferences()
    {
        var userId = AuthorAccess.UserId(User)!;
        var prefs = await _db.NotificationPreferences.FindAsync(userId)
                    ?? new NotificationPreference { UserId = userId };
        ViewData["Title"] = "تنظیمات اعلان";
        return View(prefs);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Preferences(NotificationPreference model)
    {
        var userId = AuthorAccess.UserId(User)!;
        var prefs = await _db.NotificationPreferences.FindAsync(userId);
        if (prefs is null)
        {
            prefs = new NotificationPreference { UserId = userId };
            _db.NotificationPreferences.Add(prefs);
        }

        prefs.EmailEnabled = model.EmailEnabled;
        prefs.InAppEnabled = model.InAppEnabled;
        prefs.PushEnabled = model.PushEnabled;
        prefs.SmsEnabled = model.SmsEnabled;
        prefs.NotifyNewComment = model.NotifyNewComment;
        prefs.NotifyNewFollower = model.NotifyNewFollower;
        prefs.WeeklyDigest = model.WeeklyDigest;
        prefs.PhoneE164 = string.IsNullOrWhiteSpace(model.PhoneE164) ? null : model.PhoneE164.Trim();

        await _db.SaveChangesAsync();
        TempData["PrefsSaved"] = "ذخیره شد.";
        return RedirectToAction(nameof(Preferences));
    }
}
