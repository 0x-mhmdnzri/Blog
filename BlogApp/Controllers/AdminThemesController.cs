using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

[Authorize(Roles = AppRoles.SuperAdmin)]
public class AdminThemesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IThemeService _themes;
    private readonly INotificationService _notify;
    private readonly IAuditService _audit;

    public AdminThemesController(
        ApplicationDbContext db,
        IThemeService themes,
        INotificationService notify,
        IAuditService audit)
    {
        _db = db;
        _themes = themes;
        _notify = notify;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? status)
    {
        ViewData["Title"] = "تم‌ها";
        var q = _db.CustomThemes.AsNoTracking().Include(t => t.Owner).AsQueryable();
        if (status == "pending")
            q = q.Where(t => t.Status == ThemeApprovalStatus.Pending);
        else if (status == "approved")
            q = q.Where(t => t.Status == ThemeApprovalStatus.Approved);
        else if (status == "rejected")
            q = q.Where(t => t.Status == ThemeApprovalStatus.Rejected);

        var list = await q.OrderByDescending(t => t.Status == ThemeApprovalStatus.Pending)
            .ThenByDescending(t => t.UpdatedAtUtc)
            .ToListAsync();

        ViewBag.PendingCount = await _db.CustomThemes.CountAsync(t => t.Status == ThemeApprovalStatus.Pending);
        ViewBag.Status = status;
        return View(list);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var t = await _db.CustomThemes.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        var v = ThemeContrastService.Validate(t);
        if (!v.Ok)
        {
            TempData["Msg"] = "کنتراست کافی نیست: " + string.Join(" ", v.Errors);
            return RedirectToAction(nameof(Index));
        }
        t.Status = ThemeApprovalStatus.Approved;
        t.RejectionReason = null;
        t.ReviewedAtUtc = DateTime.UtcNow;
        t.ReviewedByUserId = AuthorAccess.UserId(User);
        t.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _themes.InvalidateAsync();

        if (!string.IsNullOrEmpty(t.OwnerUserId))
        {
            await _notify.NotifyAsync(t.OwnerUserId, NotificationKind.System,
                "تم تأیید شد", $"تم «{t.Name}» تأیید شد و قابل استفاده است.", "/Themes");
        }

        await _audit.LogAsync("theme.approve", "CustomTheme", id.ToString(), t.Name, HttpContext);
        TempData["Msg"] = "تأیید شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? reason)
    {
        var t = await _db.CustomThemes.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        t.Status = ThemeApprovalStatus.Rejected;
        t.RejectionReason = string.IsNullOrWhiteSpace(reason) ? "رد توسط ادمین" : reason.Trim();
        t.ReviewedAtUtc = DateTime.UtcNow;
        t.ReviewedByUserId = AuthorAccess.UserId(User);
        t.UpdatedAtUtc = DateTime.UtcNow;
        if (t.IsActive) t.IsActive = false;
        await _db.SaveChangesAsync();
        await _themes.InvalidateAsync();

        if (!string.IsNullOrEmpty(t.OwnerUserId))
        {
            await _notify.NotifyAsync(t.OwnerUserId, NotificationKind.System,
                "تم رد شد", $"تم «{t.Name}»: {t.RejectionReason}", "/Themes");
        }

        await _audit.LogAsync("theme.reject", "CustomTheme", id.ToString(), t.RejectionReason, HttpContext);
        TempData["Msg"] = "رد شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id)
    {
        var t = await _db.CustomThemes.FirstOrDefaultAsync(x => x.Id == id && x.Status == ThemeApprovalStatus.Approved);
        if (t is null)
        {
            TempData["Msg"] = "فقط تم تأییدشده.";
            return RedirectToAction(nameof(Index));
        }
        foreach (var a in await _db.CustomThemes.Where(x => x.IsActive).ToListAsync())
            a.IsActive = false;
        t.IsActive = true;
        t.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _themes.InvalidateAsync();
        await _audit.LogAsync("theme.activate", "CustomTheme", id.ToString(), t.Name, HttpContext);
        TempData["Msg"] = $"«{t.Name}» فعال شد — کل سایت و داشبورد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var t = await _db.CustomThemes.FirstOrDefaultAsync(x => x.Id == id && !x.IsSystem);
        if (t is null) return NotFound();
        _db.CustomThemes.Remove(t);
        await _db.SaveChangesAsync();
        await _themes.InvalidateAsync();
        TempData["Msg"] = "حذف شد.";
        return RedirectToAction(nameof(Index));
    }
}
