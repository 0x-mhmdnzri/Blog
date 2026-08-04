using BlogApp.Data;
using BlogApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BlogApp.Services;

public interface ISiteConfigService
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string? value, CancellationToken ct = default);
    Task<bool> GetBoolAsync(string key, bool defaultValue = false, CancellationToken ct = default);
    Task SetBoolAsync(string key, bool value, CancellationToken ct = default);
    Task<bool> IsFlagEnabledAsync(string key, bool defaultValue = true, CancellationToken ct = default);
    Task SetFlagAsync(string key, bool enabled, CancellationToken ct = default);
    Task EnsureDefaultsAsync(CancellationToken ct = default);
    void Invalidate();
}

public sealed class SiteConfigService : ISiteConfigService
{
    private const string CacheKey = "site-config-v1";
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _config;

    public SiteConfigService(ApplicationDbContext db, IMemoryCache cache, IConfiguration config)
    {
        _db = db;
        _cache = cache;
        _config = config;
    }

    public async Task EnsureDefaultsAsync(CancellationToken ct = default)
    {
        // One-time seed only when the key is missing. After that SuperAdmin owns values in DB.
        var defaults = new Dictionary<string, string?>
        {
            [SiteSettingKeys.SiteName] = _config["Seo:SiteName"] ?? "وبلاگ",
            [SiteSettingKeys.SiteDescription] = _config["Seo:SiteDescription"] ?? "",
            [SiteSettingKeys.AuthorName] = _config["Seo:AuthorName"] ?? "",
            [SiteSettingKeys.TwitterHandle] = _config["Seo:TwitterHandle"] ?? "",
            [SiteSettingKeys.BaseUrl] = _config["Seo:BaseUrl"] ?? "",
            [SiteSettingKeys.MaintenanceMode] = "false",
            [SiteSettingKeys.MaintenanceMessage] = "سایت موقتاً در حال نگهداری است. کمی بعد برگردید.",
            [SiteSettingKeys.AnnouncementEnabled] = "false",
            [SiteSettingKeys.AnnouncementText] = "",
            [SiteSettingKeys.AnnouncementStyle] = "info",
            [SiteSettingKeys.AnnouncementVersion] = "0",

            [SiteSettingKeys.SmtpEnabled] = _config["Smtp:Enabled"] ?? "false",
            [SiteSettingKeys.SmtpHost] = _config["Smtp:Host"] ?? "",
            [SiteSettingKeys.SmtpPort] = _config["Smtp:Port"] ?? "587",
            [SiteSettingKeys.SmtpEnableSsl] = _config["Smtp:EnableSsl"] ?? "true",
            [SiteSettingKeys.SmtpUserName] = _config["Smtp:UserName"] ?? "",
            [SiteSettingKeys.SmtpPassword] = _config["Smtp:Password"] ?? "",
            [SiteSettingKeys.SmtpFromAddress] = _config["Smtp:FromAddress"] ?? "noreply@localhost",
            [SiteSettingKeys.SmtpFromDisplayName] = _config["Smtp:FromDisplayName"] ?? (_config["Seo:SiteName"] ?? "وبلاگ")
        };

        foreach (var (key, value) in defaults)
        {
            if (!await _db.SiteSettings.AnyAsync(s => s.Key == key, ct))
            {
                _db.SiteSettings.Add(new SiteSetting
                {
                    Key = key,
                    Value = value,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            }
        }

        var flags = new (string Key, string Name, string Desc, bool On)[]
        {
            (FeatureFlagKeys.Comments, "دیدگاه‌ها", "اجازه ثبت دیدگاه روی نوشته‌ها", true),
            (FeatureFlagKeys.Registration, "ثبت‌نام عمومی", "فرم ثبت‌نام خواننده فعال باشد", true),
            (FeatureFlagKeys.Bookmarks, "نشان‌گذاری", "نشان‌گذاری نوشته برای کاربران واردشده", true),
            (FeatureFlagKeys.Search, "جست‌وجو", "جست‌وجوی نوشته‌ها در صفحه اصلی", true),
            (FeatureFlagKeys.AiAssist, "کمک هوش مصنوعی", "خلاصه و پیشنهاد محتوا در ویرایشگر", true),
            (FeatureFlagKeys.PublicReports, "گزارش محتوا", "کاربران بتوانند نوشته/دیدگاه را گزارش کنند", true)
        };

        foreach (var f in flags)
        {
            if (!await _db.FeatureFlags.AnyAsync(x => x.Key == f.Key, ct))
            {
                _db.FeatureFlags.Add(new FeatureFlag
                {
                    Key = f.Key,
                    Name = f.Name,
                    Description = f.Desc,
                    IsEnabled = f.On,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        Invalidate();
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        var map = await LoadMapAsync(ct);
        return map.TryGetValue(key, out var v) ? v : null;
    }

    /// <summary>
    /// Persist a setting. Uses ExecuteUpdate so it works even when the DbContext
    /// default tracking behavior is NoTracking (Program.cs).
    /// </summary>
    public async Task SetAsync(string key, string? value, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // Direct SQL-style update — does not depend on change-tracker state.
        var updated = await _db.SiteSettings
            .Where(s => s.Key == key)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Value, value)
                .SetProperty(x => x.UpdatedAtUtc, now), ct);

        if (updated == 0)
        {
            _db.SiteSettings.Add(new SiteSetting
            {
                Key = key,
                Value = value,
                UpdatedAtUtc = now
            });
            await _db.SaveChangesAsync(ct);
        }

        Invalidate();
    }

    public async Task<bool> GetBoolAsync(string key, bool defaultValue = false, CancellationToken ct = default)
    {
        var v = await GetAsync(key, ct);
        if (string.IsNullOrWhiteSpace(v)) return defaultValue;
        return v is "1" or "true" or "True" or "yes" or "YES";
    }

    public Task SetBoolAsync(string key, bool value, CancellationToken ct = default) =>
        SetAsync(key, value ? "true" : "false", ct);

    public async Task<bool> IsFlagEnabledAsync(string key, bool defaultValue = true, CancellationToken ct = default)
    {
        var flag = await _db.FeatureFlags.AsNoTracking().FirstOrDefaultAsync(f => f.Key == key, ct);
        return flag?.IsEnabled ?? defaultValue;
    }

    public async Task SetFlagAsync(string key, bool enabled, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var updated = await _db.FeatureFlags
            .Where(f => f.Key == key)
            .ExecuteUpdateAsync(f => f
                .SetProperty(x => x.IsEnabled, enabled)
                .SetProperty(x => x.UpdatedAtUtc, now), ct);

        if (updated == 0) return;

        Invalidate();
    }

    public void Invalidate() => _cache.Remove(CacheKey);

    private async Task<Dictionary<string, string?>> LoadMapAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(CacheKey, out Dictionary<string, string?>? cached) && cached is not null)
            return cached;

        var map = await _db.SiteSettings.AsNoTracking()
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);
        _cache.Set(CacheKey, map, TimeSpan.FromMinutes(2));
        return map;
    }
}
