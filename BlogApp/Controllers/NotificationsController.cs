using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services;
using BlogApp.Services.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

[Authorize(Roles = AppRoles.Reader + "," + AppRoles.Author + "," + AppRoles.SuperAdmin)]
public class NotificationsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly NotificationHub _hub;

    public NotificationsController(ApplicationDbContext db, NotificationHub hub)
    {
        _db = db;
        _hub = hub;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = AuthorAccess.UserId(User)!;
        var items = await _db.AppNotifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(100)
            .ToListAsync();
        ViewData["Title"] = "Notifications";
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Recent(int take = 12)
    {
        var userId = AuthorAccess.UserId(User)!;
        take = Math.Clamp(take, 1, 50);
        var items = await _db.AppNotifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(take)
            .Select(n => new
            {
                n.Id,
                kind = n.Kind.ToString(),
                n.Title,
                n.Body,
                n.LinkUrl,
                n.IsRead,
                createdAtUtc = n.CreatedAtUtc
            })
            .ToListAsync();
        var unread = await _db.AppNotifications.CountAsync(n => n.UserId == userId && !n.IsRead);
        return Json(new { unread, items });
    }

    [HttpGet]
    public async Task<IActionResult> UnreadCount()
    {
        var userId = AuthorAccess.UserId(User)!;
        var count = await _db.AppNotifications.CountAsync(n => n.UserId == userId && !n.IsRead);
        return Json(new { count });
    }

    [HttpGet]
    [DisableRateLimiting]
    public async Task Stream(CancellationToken cancellationToken)
    {
        var userId = AuthorAccess.UserId(User)!;
        Response.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers["Cache-Control"] = "no-cache, no-store";
        Response.Headers["Connection"] = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no";
        Response.Headers["Content-Encoding"] = "identity";

        var buffering = HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>();
        buffering?.DisableBuffering();

        var (id, reader) = _hub.Subscribe(userId);
        try
        {
            await Response.WriteAsync("retry: 3000\n\n", cancellationToken);
            await Response.WriteAsync(": connected\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);

            var unread = await _db.AppNotifications.CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);
            await Response.WriteAsync($"data: {{\"type\":\"unread\",\"count\":{unread}}}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                using var heartbeat = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, heartbeat.Token);

                string? message = null;
                try { message = await reader.ReadAsync(linked.Token); }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }

                if (cancellationToken.IsCancellationRequested) break;

                if (message is not null)
                    await Response.WriteAsync($"data: {message}\n\n", cancellationToken);
                else
                    await Response.WriteAsync($": ping {DateTimeOffset.UtcNow.ToUnixTimeSeconds()}\n\n", cancellationToken);

                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            _hub.Unsubscribe(userId, id);
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id)
    {
        var userId = AuthorAccess.UserId(User)!;
        var n = await _db.AppNotifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (n is null) return NotFound();
        n.IsRead = true;
        await _db.SaveChangesAsync();

        if (Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
        {
            var unread = await _db.AppNotifications.CountAsync(x => x.UserId == userId && !x.IsRead);
            return Json(new { ok = true, unread });
        }

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

        if (Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            return Json(new { ok = true, unread = 0 });

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Preferences()
    {
        var userId = AuthorAccess.UserId(User)!;
        var prefs = await _db.NotificationPreferences.FindAsync(userId)
                    ?? new NotificationPreference { UserId = userId };
        ViewData["Title"] = "Notification preferences";
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
        prefs.NotifyNewPostFromFollowed = model.NotifyNewPostFromFollowed;
        prefs.PhoneE164 = string.IsNullOrWhiteSpace(model.PhoneE164) ? null : model.PhoneE164.Trim();

        await _db.SaveChangesAsync();
        TempData["PrefsSaved"] = "Saved.";
        return RedirectToAction(nameof(Preferences));
    }
}
