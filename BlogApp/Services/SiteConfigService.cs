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
            [SiteSettingKeys.AnnouncementVersion] = "0"
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

    public async Task SetAsync(string key, string? value, CancellationToken ct = default)
    {
        var row = await _db.SiteSettings.FindAsync(new object[] { key }, ct);
        if (row is null)
        {
            _db.SiteSettings.Add(new SiteSetting
            {
                Key = key,
                Value = value,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            row.Value = value;
            row.UpdatedAtUtc = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
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
        var flag = await _db.FeatureFlags.FindAsync(new object[] { key }, ct);
        if (flag is null) return;
        flag.IsEnabled = enabled;
        flag.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
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
