using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

[Authorize(Roles = AppRoles.SuperAdmin)]
public class AdminSettingsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ISiteConfigService _config;
    private readonly IAuditService _audit;

    public AdminSettingsController(ApplicationDbContext db, ISiteConfigService config, IAuditService audit)
    {
        _db = db;
        _config = config;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "تنظیمات سایت";
        var vm = new SiteSettingsViewModel
        {
            SiteName = await _config.GetAsync(SiteSettingKeys.SiteName) ?? "",
            SiteDescription = await _config.GetAsync(SiteSettingKeys.SiteDescription) ?? "",
            AuthorName = await _config.GetAsync(SiteSettingKeys.AuthorName) ?? "",
            TwitterHandle = await _config.GetAsync(SiteSettingKeys.TwitterHandle) ?? "",
            BaseUrl = await _config.GetAsync(SiteSettingKeys.BaseUrl) ?? "",
            MaintenanceMode = await _config.GetBoolAsync(SiteSettingKeys.MaintenanceMode),
            MaintenanceMessage = await _config.GetAsync(SiteSettingKeys.MaintenanceMessage) ?? "",
            AnnouncementEnabled = await _config.GetBoolAsync(SiteSettingKeys.AnnouncementEnabled),
            AnnouncementText = await _config.GetAsync(SiteSettingKeys.AnnouncementText) ?? "",
            AnnouncementStyle = await _config.GetAsync(SiteSettingKeys.AnnouncementStyle) ?? "info"
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SiteSettingsViewModel vm)
    {
        ViewData["Title"] = "تنظیمات سایت";
        if (!ModelState.IsValid) return View(vm);

        await _config.SetAsync(SiteSettingKeys.SiteName, vm.SiteName?.Trim());
        await _config.SetAsync(SiteSettingKeys.SiteDescription, vm.SiteDescription?.Trim());
        await _config.SetAsync(SiteSettingKeys.AuthorName, vm.AuthorName?.Trim());
        await _config.SetAsync(SiteSettingKeys.TwitterHandle, vm.TwitterHandle?.Trim());
        await _config.SetAsync(SiteSettingKeys.BaseUrl, vm.BaseUrl?.Trim());
        await _config.SetBoolAsync(SiteSettingKeys.MaintenanceMode, vm.MaintenanceMode);
        await _config.SetAsync(SiteSettingKeys.MaintenanceMessage, vm.MaintenanceMessage?.Trim());
        await _config.SetBoolAsync(SiteSettingKeys.AnnouncementEnabled, vm.AnnouncementEnabled);
        await _config.SetAsync(SiteSettingKeys.AnnouncementText, vm.AnnouncementText?.Trim());
        await _config.SetAsync(SiteSettingKeys.AnnouncementStyle,
            string.IsNullOrWhiteSpace(vm.AnnouncementStyle) ? "info" : vm.AnnouncementStyle.Trim().ToLowerInvariant());

        await _audit.LogAsync("settings.update", "SiteSetting", null,
            $"Maintenance={vm.MaintenanceMode}; Announcement={vm.AnnouncementEnabled}", HttpContext);

        TempData["Saved"] = "تنظیمات ذخیره شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> FeatureFlags()
    {
        ViewData["Title"] = "پرچم‌های ویژگی";
        var flags = await _db.FeatureFlags.AsNoTracking()
            .OrderBy(f => f.Name)
            .ToListAsync();
        return View(flags);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFlag(string key)
    {
        var flag = await _db.FeatureFlags.FindAsync(key);
        if (flag is null) return NotFound();

        flag.IsEnabled = !flag.IsEnabled;
        flag.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _config.Invalidate();

        await _audit.LogAsync("featureflag.toggle", "FeatureFlag", key,
            $"{flag.Name} → {(flag.IsEnabled ? "on" : "off")}", HttpContext);

        TempData["Saved"] = $"پرچم «{flag.Name}» {(flag.IsEnabled ? "فعال" : "غیرفعال")} شد.";
        return RedirectToAction(nameof(FeatureFlags));
    }
}
