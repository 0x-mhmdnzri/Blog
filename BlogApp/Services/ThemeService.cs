using BlogApp.Data;
using BlogApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BlogApp.Services;

public interface IThemeService
{
    Task<CustomTheme?> GetActiveAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CustomTheme>> ListApprovedAsync(CancellationToken ct = default);
    Task EnsureSystemThemesAsync(CancellationToken ct = default);
    Task InvalidateAsync();
}

public sealed class ThemeService : IThemeService
{
    private const string CacheKey = "active-theme-v1";
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;

    public ThemeService(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task EnsureSystemThemesAsync(CancellationToken ct = default)
    {
        if (await _db.CustomThemes.AnyAsync(t => t.IsSystem, ct))
            return;

        var dark = new CustomTheme
        {
            Name = "Dark Pro",
            Description = "تم پیش‌فرض تیره",
            IsSystem = true,
            IsActive = true,
            Status = ThemeApprovalStatus.Approved,
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
        };
        ThemeContrastService.Validate(dark);

        var light = new CustomTheme
        {
            Name = "Light Pro",
            Description = "تم روشن سیستم",
            IsSystem = true,
            IsActive = false,
            Status = ThemeApprovalStatus.Approved,
            Bg = "#f3f4f7",
            Surface = "#ffffff",
            Surface2 = "#f0f2f6",
            Border = "#d5dae3",
            Text = "#1a1d26",
            TextMuted = "#5a6478",
            Accent = "#c9922e",
            Danger = "#c9445a",
            Success = "#4a9a5c",
            Mode = "light"
        };
        ThemeContrastService.Validate(light);

        _db.CustomThemes.AddRange(dark, light);
        await _db.SaveChangesAsync(ct);
        await InvalidateAsync();
    }

    public async Task<CustomTheme?> GetActiveAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out CustomTheme? cached))
            return cached;

        var theme = await _db.CustomThemes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.IsActive && t.Status == ThemeApprovalStatus.Approved, ct);

        if (theme is null)
        {
            theme = await _db.CustomThemes.AsNoTracking()
                .FirstOrDefaultAsync(t => t.IsSystem && t.Mode == "dark", ct);
        }

        if (theme is not null)
            _cache.Set(CacheKey, theme, TimeSpan.FromMinutes(5));

        return theme;
    }

    public async Task<IReadOnlyList<CustomTheme>> ListApprovedAsync(CancellationToken ct = default) =>
        await _db.CustomThemes.AsNoTracking()
            .Where(t => t.Status == ThemeApprovalStatus.Approved)
            .OrderByDescending(t => t.IsSystem)
            .ThenBy(t => t.Name)
            .ToListAsync(ct);

    public Task InvalidateAsync()
    {
        _cache.Remove(CacheKey);
        return Task.CompletedTask;
    }
}
