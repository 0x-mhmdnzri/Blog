using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services;
using BlogApp.Services.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

[Authorize]
public class ThemesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IThemeService _themes;
    private readonly INotificationService _notify;

    public ThemesController(ApplicationDbContext db, IThemeService themes, INotificationService notify)
    {
        _db = db;
        _themes = themes;
        _notify = notify;
    }

    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> ActiveCss()
    {
        var t = await _themes.GetActiveAsync();
        if (t is null) return Content(":root{}", "text/css");
        var css = $":root{{{ThemeContrastService.ToCssVariables(t)}}}html{{color-scheme:{t.Mode}}}";
        Response.Headers.CacheControl = "public,max-age=60";
        return Content(css, "text/css; charset=utf-8");
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "تم‌های من";
        var uid = AuthorAccess.UserId(User)!;
        var mine = await _db.CustomThemes.AsNoTracking()
            .Where(t => t.OwnerUserId == uid)
            .OrderByDescending(t => t.UpdatedAtUtc)
            .ToListAsync();
        var approved = await _themes.ListApprovedAsync();
        ViewBag.Approved = approved;
        ViewBag.ActiveId = (await _themes.GetActiveAsync())?.Id;
        return View(mine);
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "تم جدید";
        return View(new CustomTheme
        {
            Name = "تم من",
            Bg = "#0b0e14",
            Surface = "#12161f",
            Surface2 = "#171c27",
            Border = "#232838",
            Text = "#e6e9f0",
            TextMuted = "#8b93a7",
            Accent = "#e3b341",
            Danger = "#e5637a",
            Success = "#9ecb8c",
            Mode = "dark"
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomTheme model, string? action)
    {
        ViewData["Title"] = "تم جدید";
        model.OwnerUserId = AuthorAccess.UserId(User);
        model.Id = 0;
        model.IsSystem = false;
        model.IsActive = false;
        model.CreatedAtUtc = DateTime.UtcNow;
        model.UpdatedAtUtc = DateTime.UtcNow;

        var v = ThemeContrastService.Validate(model);
        if (!v.Ok)
        {
            foreach (var e in v.Errors) ModelState.AddModelError(string.Empty, e);
            return View(model);
        }

        model.Status = string.Equals(action, "submit", StringComparison.OrdinalIgnoreCase)
            ? ThemeApprovalStatus.Pending
            : ThemeApprovalStatus.Draft;

        _db.CustomThemes.Add(model);
        await _db.SaveChangesAsync();

        if (model.Status == ThemeApprovalStatus.Pending)
            await NotifyAdminsAsync(model);

        TempData["Msg"] = model.Status == ThemeApprovalStatus.Pending
            ? "تم برای تأیید ارسال شد. پس از تأیید سوپرادمین قابل فعال‌سازی است."
            : "پیش‌نویس ذخیره شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int id)
    {
        var uid = AuthorAccess.UserId(User)!;
        var t = await _db.CustomThemes.FirstOrDefaultAsync(x => x.Id == id && x.OwnerUserId == uid);
        if (t is null) return NotFound();
        var v = ThemeContrastService.Validate(t);
        if (!v.Ok)
        {
            TempData["Err"] = string.Join(" ", v.Errors);
            return RedirectToAction(nameof(Index));
        }
        t.Status = ThemeApprovalStatus.Pending;
        t.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await NotifyAdminsAsync(t);
        TempData["Msg"] = "برای تأیید ارسال شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var uid = AuthorAccess.UserId(User)!;
        var t = await _db.CustomThemes.FirstOrDefaultAsync(x => x.Id == id && x.OwnerUserId == uid && !x.IsSystem);
        if (t is null) return NotFound();
        if (t.IsActive)
        {
            TempData["Err"] = "تم فعال را نمی‌توان حذف کرد.";
            return RedirectToAction(nameof(Index));
        }
        _db.CustomThemes.Remove(t);
        await _db.SaveChangesAsync();
        TempData["Msg"] = "حذف شد.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Activate an approved theme (staff/super only for site-wide).</summary>
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Activate(int id)
    {
        var t = await _db.CustomThemes.FirstOrDefaultAsync(x => x.Id == id && x.Status == ThemeApprovalStatus.Approved);
        if (t is null)
        {
            TempData["Err"] = "فقط تم تأییدشده قابل فعال‌سازی است.";
            return RedirectToAction(nameof(Index));
        }

        var all = await _db.CustomThemes.Where(x => x.IsActive).ToListAsync();
        foreach (var a in all) a.IsActive = false;
        t.IsActive = true;
        t.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _themes.InvalidateAsync();
        TempData["Msg"] = $"تم «{t.Name}» در کل سایت فعال شد.";
        return RedirectToAction(nameof(Index));
    }

    private async Task NotifyAdminsAsync(CustomTheme model)
    {
        try
        {
            var supers = await _db.Users.AsNoTracking()
                .Where(u => _db.UserRoles.Any(ur =>
                    ur.UserId == u.Id &&
                    _db.Roles.Any(r => r.Id == ur.RoleId && r.Name == AppRoles.SuperAdmin)))
                .Select(u => u.Id)
                .ToListAsync();
            var name = User.Identity?.Name ?? "user";
            foreach (var sid in supers)
            {
                await _notify.NotifyAsync(sid, NotificationKind.System,
                    "درخواست تم جدید",
                    $"{name} تم «{model.Name}» را برای تأیید فرستاد.",
                    "/AdminThemes");
            }
        }
        catch { /* non-fatal */ }
    }
}
