using System.Text;
using System.Text.Json;
using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services;
using BlogApp.Services.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public class ThemesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IThemeService _themes;
    private readonly INotificationService _notify;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public ThemesController(ApplicationDbContext db, IThemeService themes, INotificationService notify)
    {
        _db = db;
        _themes = themes;
        _notify = notify;
    }

    private int? ReadPreferredThemeId()
    {
        if (Request.Cookies.TryGetValue(ThemeService.PreferenceCookie, out var raw)
            && int.TryParse(raw, out var id) && id > 0)
            return id;
        return null;
    }

    private void WritePreferredThemeCookie(int themeId)
    {
        Response.Cookies.Append(ThemeService.PreferenceCookie, themeId.ToString(), new CookieOptions
        {
            Path = "/",
            HttpOnly = false,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            MaxAge = TimeSpan.FromDays(365),
            Expires = DateTimeOffset.UtcNow.AddDays(365)
        });
    }

    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> ActiveCss()
    {
        var t = await _themes.ResolveForVisitorAsync(ReadPreferredThemeId());
        if (t is null) return Content(":root{}", "text/css");
        var css = $":root{{{ThemeContrastService.ToCssVariables(t)}}}html{{color-scheme:{t.Mode}}}";
        Response.Headers.CacheControl = "private,max-age=30";
        return Content(css, "text/css; charset=utf-8");
    }

    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "تم‌ها";
        var approved = await _themes.ListApprovedAsync();
        ViewBag.Approved = approved;
        ViewBag.ActiveId = (await _themes.GetActiveAsync())?.Id;
        ViewBag.PreferredId = ReadPreferredThemeId();

        List<CustomTheme> mine = new();
        if (User.Identity?.IsAuthenticated == true)
        {
            var uid = AuthorAccess.UserId(User);
            if (!string.IsNullOrEmpty(uid))
            {
                mine = await _db.CustomThemes.AsNoTracking()
                    .Where(t => t.OwnerUserId == uid)
                    .OrderByDescending(t => t.UpdatedAtUtc)
                    .ToListAsync();
            }
        }

        return View(mine);
    }

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Select(int id, string? returnUrl = null)
    {
        var t = await _themes.GetApprovedByIdAsync(id);
        if (t is null && User.Identity?.IsAuthenticated == true)
        {
            var uid = AuthorAccess.UserId(User);
            t = await _db.CustomThemes.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.OwnerUserId == uid);
        }

        if (t is null)
        {
            TempData["Err"] = "تم یافت نشد یا هنوز برای عموم تأیید نشده است.";
            return RedirectToAction(nameof(Index));
        }

        WritePreferredThemeCookie(t.Id);
        TempData["Msg"] = $"تم «{t.Name}» برای شما فعال شد.";

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public IActionResult ClearPreference(string? returnUrl = null)
    {
        Response.Cookies.Delete(ThemeService.PreferenceCookie, new CookieOptions { Path = "/" });
        TempData["Msg"] = "تم شخصی پاک شد — تم پیش‌فرض سایت اعمال می‌شود.";
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet, Authorize]
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

    [HttpPost, Authorize, ValidateAntiForgeryToken]
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

        WritePreferredThemeCookie(model.Id);

        TempData["Msg"] = model.Status == ThemeApprovalStatus.Pending
            ? "تم برای تأیید ارسال شد و برای شما اعمال شد."
            : "پیش‌نویس ذخیره شد و برای شما اعمال شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    [RequestSizeLimit(64 * 1024)]
    public async Task<IActionResult> ImportFile(IFormFile? file, string? action)
    {
        if (file is null || file.Length == 0)
        {
            TempData["Err"] = "فایلی انتخاب نشده است.";
            return RedirectToAction(nameof(Create));
        }

        var name = file.FileName ?? "";
        if (!name.EndsWith(ThemeService.FileExtension, StringComparison.OrdinalIgnoreCase)
            && !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Err"] = $"فقط فایل {ThemeService.FileExtension} (یا JSON) مجاز است.";
            return RedirectToAction(nameof(Create));
        }

        if (file.Length > 64 * 1024)
        {
            TempData["Err"] = "حجم فایل حداکثر ۶۴KB.";
            return RedirectToAction(nameof(Create));
        }

        string json;
        await using (var stream = file.OpenReadStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8))
            json = await reader.ReadToEndAsync();

        ThemePack? pack;
        try
        {
            pack = JsonSerializer.Deserialize<ThemePack>(json, JsonOpts);
        }
        catch (Exception ex)
        {
            TempData["Err"] = "JSON نامعتبر: " + ex.Message;
            return RedirectToAction(nameof(Create));
        }

        if (pack is null || string.IsNullOrWhiteSpace(pack.Name))
        {
            TempData["Err"] = "فایل تم نامعتبر است (name الزامی است).";
            return RedirectToAction(nameof(Create));
        }

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
            OwnerUserId = AuthorAccess.UserId(User),
            IsSystem = false,
            IsActive = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var v = ThemeContrastService.Validate(entity);
        if (!v.Ok)
        {
            TempData["Err"] = string.Join(" ", v.Errors);
            return RedirectToAction(nameof(Create));
        }

        entity.Status = string.Equals(action, "submit", StringComparison.OrdinalIgnoreCase)
            ? ThemeApprovalStatus.Pending
            : ThemeApprovalStatus.Draft;

        _db.CustomThemes.Add(entity);
        await _db.SaveChangesAsync();

        if (entity.Status == ThemeApprovalStatus.Pending)
            await NotifyAdminsAsync(entity);

        WritePreferredThemeCookie(entity.Id);

        TempData["Msg"] = entity.Status == ThemeApprovalStatus.Pending
            ? $"فایل «{entity.Name}» بارگذاری و برای تأیید ارسال شد. فعلاً برای شما اعمال شده است."
            : $"فایل «{entity.Name}» به‌عنوان پیش‌نویس ذخیره شد و برای شما اعمال شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
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

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var uid = AuthorAccess.UserId(User)!;
        var t = await _db.CustomThemes.FirstOrDefaultAsync(x => x.Id == id && x.OwnerUserId == uid && !x.IsSystem);
        if (t is null) return NotFound();
        if (t.IsActive)
        {
            TempData["Err"] = "تم فعال سایت را نمی‌توان حذف کرد.";
            return RedirectToAction(nameof(Index));
        }
        _db.CustomThemes.Remove(t);
        await _db.SaveChangesAsync();
        TempData["Msg"] = "حذف شد.";
        return RedirectToAction(nameof(Index));
    }

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
