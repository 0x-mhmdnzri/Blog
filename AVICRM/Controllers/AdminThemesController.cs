using System.Text;
using System.Text.Json;
using AVICRM.Data;
using AVICRM.Models;
using AVICRM.Services;
using AVICRM.Services.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AVICRM.Controllers;

/// <summary>
/// Themes management in admin panel.
/// Authors: create / upload own themes (pending SuperAdmin approval).
/// SuperAdmin: approve / reject / activate site-wide / reimport packs.
/// </summary>
[Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
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

    private bool IsSuper => User.IsInRole(AppRoles.SuperAdmin);

    [HttpGet]
    public async Task<IActionResult> Index(string? status)
    {
        ViewData["Title"] = "تم‌ها";
        ViewBag.IsSuper = IsSuper;

        if (IsSuper)
        {
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

        // Author: only own themes
        var uid = AuthorAccess.UserId(User)!;
        var mine = await _db.CustomThemes.AsNoTracking()
            .Where(t => t.OwnerUserId == uid)
            .OrderByDescending(t => t.UpdatedAtUtc)
            .ToListAsync();
        ViewBag.PendingCount = mine.Count(t => t.Status == ThemeApprovalStatus.Pending);
        ViewBag.Status = status;
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
        var uid = AuthorAccess.UserId(User)!;
        model.OwnerUserId = uid;
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

        // Authors always submit as Pending (need SuperAdmin approval for public gallery).
        // SuperAdmin may save as Approved draft-system optional via action.
        if (IsSuper && string.Equals(action, "approve", StringComparison.OrdinalIgnoreCase))
        {
            model.Status = ThemeApprovalStatus.Approved;
            model.ReviewedAtUtc = DateTime.UtcNow;
            model.ReviewedByUserId = uid;
        }
        else if (string.Equals(action, "draft", StringComparison.OrdinalIgnoreCase))
        {
            model.Status = ThemeApprovalStatus.Draft;
        }
        else
        {
            model.Status = ThemeApprovalStatus.Pending;
        }

        _db.CustomThemes.Add(model);
        await _db.SaveChangesAsync();

        if (model.Status == ThemeApprovalStatus.Pending)
            await NotifySupersAsync(model);

        await _audit.LogAsync("theme.create", "CustomTheme", model.Id.ToString(), model.Name, HttpContext);
        TempData["Msg"] = model.Status == ThemeApprovalStatus.Pending
            ? "تم ساخته شد و برای تأیید سوپرادمین ارسال شد."
            : model.Status == ThemeApprovalStatus.Approved
                ? "تم ساخته و تأیید شد."
                : "پیش‌نویس ذخیره شد.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Author / SuperAdmin upload .blogtheme as own theme (Pending unless SuperAdmin).</summary>
    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(64 * 1024)]
    public async Task<IActionResult> ImportFile(IFormFile? file, string? action)
    {
        if (file is null || file.Length == 0)
        {
            TempData["Msg"] = "فایلی انتخاب نشده.";
            return RedirectToAction(nameof(Index));
        }

        var name = file.FileName ?? "";
        if (!name.EndsWith(ThemeService.FileExtension, StringComparison.OrdinalIgnoreCase)
            && !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Msg"] = $"فقط فایل {ThemeService.FileExtension} (یا JSON) مجاز است.";
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

        if (pack is null || string.IsNullOrWhiteSpace(pack.Name))
        {
            TempData["Msg"] = "فایل تم نامعتبر است.";
            return RedirectToAction(nameof(Index));
        }

        var uid = AuthorAccess.UserId(User)!;
        var entity = new CustomTheme
        {
            Name = pack.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(pack.Description) ? null : pack.Description.Trim(),
            Bg = pack.Bg,
            Surface = pack.Surface,
            Surface2 = pack.Surface2,
            Border = pack.Border,
            Text = pack.Text,
            TextMuted = pack.TextMuted,
            Accent = pack.Accent,
            Danger = pack.Danger,
            Success = pack.Success,
            Mode = string.IsNullOrWhiteSpace(pack.Mode) ? "dark" : pack.Mode.Trim().ToLowerInvariant(),
            OwnerUserId = uid,
            IsSystem = false,
            IsActive = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var v = ThemeContrastService.Validate(entity);
        if (!v.Ok)
        {
            TempData["Msg"] = "کنتراست کافی نیست: " + string.Join(" ", v.Errors);
            return RedirectToAction(nameof(Index));
        }

        if (IsSuper && string.Equals(action, "approve", StringComparison.OrdinalIgnoreCase))
        {
            entity.Status = ThemeApprovalStatus.Approved;
            entity.ReviewedAtUtc = DateTime.UtcNow;
            entity.ReviewedByUserId = uid;
        }
        else if (string.Equals(action, "draft", StringComparison.OrdinalIgnoreCase))
        {
            entity.Status = ThemeApprovalStatus.Draft;
        }
        else
        {
            entity.Status = ThemeApprovalStatus.Pending;
        }

        _db.CustomThemes.Add(entity);
        await _db.SaveChangesAsync();

        if (entity.Status == ThemeApprovalStatus.Pending)
            await NotifySupersAsync(entity);

        await _audit.LogAsync("theme.import_own", "CustomTheme", entity.Id.ToString(), entity.Name, HttpContext);
        TempData["Msg"] = entity.Status == ThemeApprovalStatus.Pending
            ? $"«{entity.Name}» بارگذاری و برای تأیید ارسال شد."
            : $"«{entity.Name}» ذخیره شد.";
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
            TempData["Msg"] = string.Join(" ", v.Errors);
            return RedirectToAction(nameof(Index));
        }

        t.Status = ThemeApprovalStatus.Pending;
        t.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await NotifySupersAsync(t);
        TempData["Msg"] = "برای تأیید ارسال شد.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Delete own theme (authors) or any non-system (SuperAdmin).</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var uid = AuthorAccess.UserId(User)!;
        CustomTheme? t;
        if (IsSuper)
            t = await _db.CustomThemes.FirstOrDefaultAsync(x => x.Id == id && !x.IsSystem);
        else
            t = await _db.CustomThemes.FirstOrDefaultAsync(x => x.Id == id && x.OwnerUserId == uid && !x.IsSystem);

        if (t is null) return NotFound();
        if (t.IsActive)
        {
            TempData["Msg"] = "تم فعال سایت را نمی‌توان حذف کرد.";
            return RedirectToAction(nameof(Index));
        }

        _db.CustomThemes.Remove(t);
        await _db.SaveChangesAsync();
        await _themes.InvalidateAsync();
        TempData["Msg"] = "حذف شد.";
        return RedirectToAction(nameof(Index));
    }

    // ── SuperAdmin only ──────────────────────────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Reimport()
    {
        var result = await _themes.ImportFromDirectoryAsync();
        await _audit.LogAsync("theme.reimport", "CustomTheme", null,
            $"+{result.Imported} ~{result.Updated} skip {result.Skipped}", HttpContext);
        TempData["Msg"] =
            $"واردات فایل‌ها: جدید {result.Imported}، به‌روز {result.Updated}، رد {result.Skipped}.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>SuperAdmin system pack import (OwnerUserId null, Approved).</summary>
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    [RequestSizeLimit(64 * 1024)]
    public async Task<IActionResult> ImportSystemPack(IFormFile? file)
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
    [Authorize(Roles = AppRoles.SuperAdmin)]
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
    [Authorize(Roles = AppRoles.SuperAdmin)]
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
                "تم رد شد", $"تم «{t.Name}»: {t.RejectionReason}", "/AdminThemes");
        }

        await _audit.LogAsync("theme.reject", "CustomTheme", id.ToString(), t.RejectionReason, HttpContext);
        TempData["Msg"] = "رد شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
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

    private async Task NotifySupersAsync(CustomTheme model)
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
                    "/AdminThemes?status=pending");
            }
        }
        catch { /* non-fatal */ }
    }
}
