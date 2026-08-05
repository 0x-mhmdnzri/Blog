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
            .Concat(UiTranslationCatalog.Seo)
            .Concat(UiTranslationCatalog.Media)
            .Concat(UiTranslationCatalog.Monetization)
            .Concat(UiTranslationCatalog.Newsletter)
            .Concat(UiTranslationCatalog.Author)
            .Concat(UiTranslationCatalog.Accessibility)
            .Concat(UiTranslationCatalog.Backup)
            .Concat(UiTranslationCatalog.Enterprise)
            .Concat(UiTranslationCatalog.Moderation)
            .Concat(UiTranslationCatalog.Marketing)
            .Concat(UiTranslationCatalog.Search)
            .Concat(UiTranslationCatalog.Auth)
            .Concat(UiTranslationCatalog.NavExtra)
            .Concat(UiTranslationCatalog.Themes)
            .Concat(UiTranslationCatalog.UsersRoles)
            .Concat(UiTranslationCatalog.ApiKeysAdmin)
            .Concat(UiTranslationCatalog.Report);

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

        if (added > 0)
            await _db.SaveChangesAsync(ct);
    }
}
