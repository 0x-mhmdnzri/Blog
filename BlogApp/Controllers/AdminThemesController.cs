using System.Text;
using System.Text.Json;
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

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

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

    /// <summary>Re-scan ContentRoot/themes/*.blogtheme into DB.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reimport()
    {
        var result = await _themes.ImportFromDirectoryAsync();
        await _audit.LogAsync("theme.reimport", "CustomTheme", null,
            $"+{result.Imported} ~{result.Updated} skip {result.Skipped}", HttpContext);
        TempData["Msg"] =
            $"واردات فایل‌ها: جدید {result.Imported}، به‌روز {result.Updated}، رد {result.Skipped}.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Upload one .blogtheme file and import.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(64 * 1024)]
    public async Task<IActionResult> ImportFile(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            TempData["Msg"] = "فایلی انتخاب نشده.";
            return RedirectToAction(nameof(Index));
        }

        var name = file.FileName ?? "";
        if (!name.EndsWith(ThemeService.FileExtension, StringComparison.OrdinalIgnoreCase))
        {
            TempData["Msg"] = $"فقط فایل {ThemeService.FileExtension} مجاز است.";
            return RedirectToAction(nameof(Index));
        }

        if (file.Length > 64 * 1024)
        {
            TempData["Msg"] = "حجم فایل زیاد است (حداکثر ۶۴KB).";
            return RedirectToAction(nameof(Index));
        }

        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var json = await reader.ReadToEndAsync();

        ThemePack? pack;
        try
        {
            pack = JsonSerializer.Deserialize<ThemePack>(json, JsonOpts);
        }
        catch (Exception ex)
        {
            TempData["Msg"] = "JSON نامعتبر: " + ex.Message;
            return RedirectToAction(nameof(Index));
        }

        if (pack is null)
        {
            TempData["Msg"] = "JSON خالی است.";
            return RedirectToAction(nameof(Index));
        }

        var key = !string.IsNullOrWhiteSpace(pack.Id)
            ? pack.Id.Trim()
            : Path.GetFileNameWithoutExtension(name);

        var result = await _themes.ImportPackAsync(pack, key);
        await _audit.LogAsync("theme.import_file", "CustomTheme", result.ThemeId?.ToString(), result.Message, HttpContext);
        TempData["Msg"] = result.Ok ? result.Message : "خطا: " + result.Message;
        return RedirectToAction(nameof(Index));
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
