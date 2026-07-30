using BlogApp.Data;
using BlogApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BlogApp.Services;

public interface IUiTranslator
{
    string this[string key] { get; }
    string T(string key, string? languageCode = null);
    Task InvalidateCacheAsync();
    Task EnsureSeedAsync(CancellationToken ct = default);
}

public sealed partial class UiTranslatorService : IUiTranslator
{
    private const string CacheKeyPrefix = "ui-i18n:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);

    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ICultureService _culture;

    public UiTranslatorService(
        ApplicationDbContext db,
        IMemoryCache cache,
        ICultureService culture)
    {
        _db = db;
        _cache = cache;
        _culture = culture;
    }

    public string this[string key] => T(key);

    public string T(string key, string? languageCode = null)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;

        var lang = AppCultures.Normalize(languageCode ?? _culture.CurrentCode);
        var map = GetMap(lang);

        if (map.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
            return value;

        if (lang != AppCultures.Default)
        {
            var fallback = GetMap(AppCultures.Default);
            if (fallback.TryGetValue(key, out var fb) && !string.IsNullOrEmpty(fb))
                return fb;
        }

        return key.Contains('.') ? key[(key.LastIndexOf('.') + 1)..] : key;
    }

    private IReadOnlyDictionary<string, string> GetMap(string lang)
    {
        return _cache.GetOrCreate(CacheKeyPrefix + lang, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return _db.UiTranslations.AsNoTracking()
                .Where(t => t.LanguageCode == lang)
                .ToDictionary(t => t.Key, t => t.Value, StringComparer.OrdinalIgnoreCase);
        }) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public Task InvalidateCacheAsync()
    {
        foreach (var c in AppCultures.All)
            _cache.Remove(CacheKeyPrefix + c.Code);
        return Task.CompletedTask;
    }
}
