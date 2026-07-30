using BlogApp.Data;
using BlogApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Services;

public sealed partial class UiTranslatorService
{
    public async Task EnsureSeedAsync(CancellationToken ct = default)
    {
        var existing = await _db.UiTranslations.AsNoTracking()
            .Select(t => t.Key + "|" + t.LanguageCode)
            .ToListAsync(ct);
        var set = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        var insertRows = UiTranslationCatalog.All
            .Concat(UiTranslationCatalog.Wizard)
            .Concat(UiTranslationCatalog.Analytics)
            .Concat(UiTranslationCatalog.Taxonomy)
            .Concat(UiTranslationCatalog.Sidebar)
            .Concat(UiTranslationCatalog.Errors)
            .Concat(UiTranslationCatalog.Seo);

        var added = 0;
        foreach (var (key, group, fa, en, ar) in insertRows)
        {
            foreach (var (code, value) in new[] { ("fa", fa), ("en", en), ("ar", ar) })
            {
                var id = key + "|" + code;
                if (set.Contains(id)) continue;
                _db.UiTranslations.Add(new UiTranslation
                {
                    Key = key,
                    LanguageCode = code,
                    Value = value,
                    Group = group,
                    UpdatedAtUtc = DateTime.UtcNow
                });
                set.Add(id);
                added++;
            }
        }

        var tracked = await _db.UiTranslations
            .Where(t => t.Group == "ana" || t.Key == "admin.nav.analytics")
            .ToListAsync(ct);

        var byId = tracked.ToDictionary(t => t.Key + "|" + t.LanguageCode, StringComparer.OrdinalIgnoreCase);
        var changed = 0;
        foreach (var (key, group, fa, en, ar) in UiTranslationCatalog.Analytics)
        {
            foreach (var (code, value) in new[] { ("fa", fa), ("en", en), ("ar", ar) })
            {
                var id = key + "|" + code;
                if (byId.TryGetValue(id, out var row))
                {
                    if (row.Value != value)
                    {
                        row.Value = value;
                        row.Group = group;
                        row.UpdatedAtUtc = DateTime.UtcNow;
                        changed++;
                    }
                }
                else if (!set.Contains(id))
                {
                    _db.UiTranslations.Add(new UiTranslation
                    {
                        Key = key,
                        LanguageCode = code,
                        Value = value,
                        Group = group,
                        UpdatedAtUtc = DateTime.UtcNow
                    });
                    changed++;
                }
            }
        }

        if (added > 0 || changed > 0)
            await _db.SaveChangesAsync(ct);

        await InvalidateCacheAsync();
    }
}
