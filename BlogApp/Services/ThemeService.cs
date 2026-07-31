using System.Text.Json;
using BlogApp.Data;
using BlogApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlogApp.Services;

public interface IThemeService
{
    Task<CustomTheme?> GetActiveAsync(CancellationToken ct = default);
    Task<CustomTheme?> GetApprovedByIdAsync(int id, CancellationToken ct = default);
    Task<CustomTheme?> GetByIdAsync(int id, CancellationToken ct = default);
    /// <summary>Preferred cookie theme if found, otherwise site-wide active.</summary>
    Task<CustomTheme?> ResolveForVisitorAsync(int? preferredId, CancellationToken ct = default);
    Task<IReadOnlyList<CustomTheme>> ListApprovedAsync(CancellationToken ct = default);
    Task EnsureSystemThemesAsync(CancellationToken ct = default);
    Task<ThemeImportResult> ImportFromDirectoryAsync(string? directory = null, CancellationToken ct = default);
    Task<ThemeImportItemResult> ImportPackAsync(ThemePack pack, string? sourceKey = null, CancellationToken ct = default);
    Task InvalidateAsync();
}

public sealed record ThemeImportResult(int Imported, int Updated, int Skipped, IReadOnlyList<string> Messages);
public sealed record ThemeImportItemResult(bool Ok, string Message, int? ThemeId = null, bool Created = false);

public sealed class ThemeService : IThemeService
{
    public const string FileExtension = ".blogtheme";
    public const string PreferenceCookie = "Blog.ThemeId";
    private const string CacheKey = "active-theme-v1";
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly IHostEnvironment _env;
    private readonly ILogger<ThemeService> _log;

    public ThemeService(
        ApplicationDbContext db,
        IMemoryCache cache,
        IHostEnvironment env,
        ILogger<ThemeService> log)
    {
        _db = db;
        _cache = cache;
        _env = env;
        _log = log;
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

    public async Task<CustomTheme?> GetApprovedByIdAsync(int id, CancellationToken ct = default) =>
        await _db.CustomThemes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id && t.Status == ThemeApprovalStatus.Approved, ct);

    public async Task<CustomTheme?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await _db.CustomThemes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<CustomTheme?> ResolveForVisitorAsync(int? preferredId, CancellationToken ct = default)
    {
        if (preferredId is > 0)
        {
            // Cookie preference: any theme the visitor selected (approved or own draft preview)
            var preferred = await GetByIdAsync(preferredId.Value, ct);
            if (preferred is not null)
                return preferred;
        }
        return await GetActiveAsync(ct);
    }

    public async Task<ThemeImportResult> ImportFromDirectoryAsync(string? directory = null, CancellationToken ct = default)
    {
        var dir = string.IsNullOrWhiteSpace(directory)
            ? Path.Combine(_env.ContentRootPath, "themes")
            : directory;

        var messages = new List<string>();
        if (!Directory.Exists(dir))
        {
            messages.Add($"themes folder missing: {dir}");
            return new ThemeImportResult(0, 0, 0, messages);
        }

        var files = Directory.GetFiles(dir, "*" + FileExtension, SearchOption.TopDirectoryOnly);
        if (files.Length == 0)
        {
            messages.Add($"no {FileExtension} files in {dir}");
            return new ThemeImportResult(0, 0, 0, messages);
        }

        int imported = 0, updated = 0, skipped = 0;
        foreach (var path in files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var json = await File.ReadAllTextAsync(path, ct);
                var pack = JsonSerializer.Deserialize<ThemePack>(json, JsonOpts);
                if (pack is null)
                {
                    skipped++;
                    messages.Add($"{Path.GetFileName(path)}: invalid JSON");
                    continue;
                }

                var key = !string.IsNullOrWhiteSpace(pack.Id)
                    ? pack.Id.Trim()
                    : Path.GetFileNameWithoutExtension(path);

                var result = await ImportPackAsync(pack, key, ct);
                if (!result.Ok)
                {
                    skipped++;
                    messages.Add($"{Path.GetFileName(path)}: {result.Message}");
                    continue;
                }

                if (result.Created) imported++;
                else updated++;
                messages.Add($"{Path.GetFileName(path)}: {result.Message}");
            }
            catch (Exception ex)
            {
                skipped++;
                messages.Add($"{Path.GetFileName(path)}: {ex.Message}");
                _log.LogWarning(ex, "Theme import failed for {Path}", path);
            }
        }

        _log.LogInformation(
            "Theme packs: imported={Imported} updated={Updated} skipped={Skipped} dir={Dir}",
            imported, updated, skipped, dir);
        return new ThemeImportResult(imported, updated, skipped, messages);
    }

    public async Task<ThemeImportItemResult> ImportPackAsync(ThemePack pack, string? sourceKey = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pack.Name))
            return new ThemeImportItemResult(false, "name is required");

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
            IsSystem = pack.IsSystem,
            OwnerUserId = null,
            Status = ThemeApprovalStatus.Approved,
            IsActive = false
        };

        var v = ThemeContrastService.Validate(entity);
        if (!v.Ok)
            return new ThemeImportItemResult(false, "contrast failed: " + string.Join("; ", v.Errors));

        var key = (sourceKey ?? pack.Id ?? pack.Name).Trim();
        var marker = "[pack:" + key + "]";

        var existing = await _db.CustomThemes
            .FirstOrDefaultAsync(t =>
                t.OwnerUserId == null &&
                t.Description != null &&
                t.Description.Contains(marker),
                ct);

        if (existing is null)
        {
            existing = await _db.CustomThemes
                .FirstOrDefaultAsync(t => t.OwnerUserId == null && t.Name == entity.Name, ct);
        }

        var desc = entity.Description ?? "";
        if (!desc.Contains(marker, StringComparison.Ordinal))
            entity.Description = string.IsNullOrEmpty(desc) ? marker : desc + " " + marker;

        if (existing is null)
        {
            entity.CreatedAtUtc = DateTime.UtcNow;
            entity.UpdatedAtUtc = DateTime.UtcNow;
            entity.ReviewedAtUtc = DateTime.UtcNow;
            _db.CustomThemes.Add(entity);
            await _db.SaveChangesAsync(ct);

            if (pack.Activate)
                await ActivateInternalAsync(entity.Id, ct);

            await InvalidateAsync();
            return new ThemeImportItemResult(true, $"created «{entity.Name}»", entity.Id, Created: true);
        }

        existing.Name = entity.Name;
        existing.Description = entity.Description;
        existing.Bg = entity.Bg;
        existing.Surface = entity.Surface;
        existing.Surface2 = entity.Surface2;
        existing.Border = entity.Border;
        existing.Text = entity.Text;
        existing.TextMuted = entity.TextMuted;
        existing.Accent = entity.Accent;
        existing.Danger = entity.Danger;
        existing.Success = entity.Success;
        existing.Mode = entity.Mode;
        existing.ContrastTextOnBg = entity.ContrastTextOnBg;
        existing.ContrastMutedOnBg = entity.ContrastMutedOnBg;
        existing.ContrastAccentOnBg = entity.ContrastAccentOnBg;
        existing.IsSystem = entity.IsSystem;
        existing.Status = ThemeApprovalStatus.Approved;
        existing.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        if (pack.Activate)
            await ActivateInternalAsync(existing.Id, ct);

        await InvalidateAsync();
        return new ThemeImportItemResult(true, $"updated «{existing.Name}»", existing.Id, Created: false);
    }

    private async Task ActivateInternalAsync(int id, CancellationToken ct)
    {
        var all = await _db.CustomThemes.Where(x => x.IsActive).ToListAsync(ct);
        foreach (var a in all) a.IsActive = false;
        var t = await _db.CustomThemes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is not null)
        {
            t.IsActive = true;
            t.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
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
