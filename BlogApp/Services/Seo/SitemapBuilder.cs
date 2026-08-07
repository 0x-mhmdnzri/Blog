using System.Text;
using BlogApp.Data;
using BlogApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Services.Seo;

/// <summary>
/// P1.3 — builds clean, split XML sitemaps: only canonical indexable 200-class URLs
/// with accurate lastmod. Sitemap index + posts / pages / authors / taxonomies.
/// </summary>
public static class SitemapBuilder
{
    public const int MaxUrlsPerFile = 40_000; // protocol limit 50k; stay under

    public static string FormatLastMod(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ");

    public static DateTime BestLastMod(DateTime updatedAt, DateTime? publishedAt) =>
        publishedAt.HasValue && publishedAt.Value > updatedAt ? publishedAt.Value : updatedAt;

    public static Task<string> BuildIndexAsync(string baseUrl)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<sitemapindex xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
        foreach (var name in new[] { "pages", "posts", "authors", "taxonomies" })
        {
            sb.AppendLine("  <sitemap>");
            sb.AppendLine($"    <loc>{Escape($"{baseUrl}/sitemap-{name}.xml")}</loc>");
            sb.AppendLine($"    <lastmod>{FormatLastMod(DateTime.UtcNow)}</lastmod>");
            sb.AppendLine("  </sitemap>");
        }
        sb.AppendLine("</sitemapindex>");
        return Task.FromResult(sb.ToString());
    }

    public static Task<string> BuildPagesAsync(string baseUrl)
    {
        var sb = StartUrlset();
        AppendUrl(sb, $"{baseUrl}/", DateTime.UtcNow, "daily", "1.0");
        foreach (var lang in AppCultures.All.Select(c => c.Code))
            AppendUrl(sb, $"{baseUrl}/{lang}/", DateTime.UtcNow, "daily", "0.9");

        foreach (var page in new[] { "about", "services", "projects", "contact" })
        {
            foreach (var lang in AppCultures.All.Select(c => c.Code))
                AppendUrl(sb, $"{baseUrl}/{lang}/pages/{page}", null, "monthly", "0.6");
        }

        EndUrlset(sb);
        return Task.FromResult(sb.ToString());
    }

    public static async Task<string> BuildPostsAsync(ApplicationDbContext db, string baseUrl, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var posts = await db.Posts.AsNoTracking()
            .Where(p => p.IsPublished && !p.IsDeleted)
            .Where(p => p.ExpiresAtUtc == null || p.ExpiresAtUtc > now)
            .Where(p => p.ScheduledPublishAtUtc == null || p.ScheduledPublishAtUtc <= now)
            .Where(p => p.TranslationStatus == TranslationStatus.Original
                        || p.TranslationStatus == TranslationStatus.Approved)
            .Select(p => new
            {
                p.Id,
                p.Slug,
                p.UpdatedAtUtc,
                p.PublishedAtUtc,
                p.LanguageCode,
                GroupId = p.TranslationGroupId ?? p.Id
            })
            .OrderByDescending(p => p.UpdatedAtUtc)
            .Take(MaxUrlsPerFile)
            .ToListAsync(ct);

        var byGroup = posts.GroupBy(p => p.GroupId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var sb = StartUrlset();
        foreach (var post in posts)
        {
            var loc = $"{baseUrl}/{post.LanguageCode}/post/{post.Slug}";
            var last = BestLastMod(post.UpdatedAtUtc, post.PublishedAtUtc);

            List<(string Lang, string Href)>? alts = null;
            if (byGroup.TryGetValue(post.GroupId, out var siblings) && siblings.Count > 1)
            {
                alts = siblings
                    .Select(s => (s.LanguageCode, $"{baseUrl}/{s.LanguageCode}/post/{s.Slug}"))
                    .ToList();
                var def = siblings.FirstOrDefault(s => s.LanguageCode == "fa") ?? siblings[0];
                alts.Add(("x-default", $"{baseUrl}/{def.LanguageCode}/post/{def.Slug}"));
            }

            AppendUrl(sb, loc, last, "monthly", "0.8", alts);
        }
        EndUrlset(sb);
        return sb.ToString();
    }

    public static async Task<string> BuildAuthorsAsync(ApplicationDbContext db, string baseUrl, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var authors = await db.Posts.AsNoTracking()
            .Where(p => p.IsPublished && !p.IsDeleted)
            .Where(p => p.ExpiresAtUtc == null || p.ExpiresAtUtc > now)
            .Where(p => p.Author != null && p.Author.UserName != null)
            .GroupBy(p => p.Author!.UserName!)
            .Select(g => new
            {
                UserName = g.Key,
                LastMod = g.Max(x => x.UpdatedAtUtc)
            })
            .OrderBy(a => a.UserName)
            .Take(MaxUrlsPerFile)
            .ToListAsync(ct);

        var sb = StartUrlset();
        foreach (var a in authors)
            AppendUrl(sb, $"{baseUrl}/author/{Uri.EscapeDataString(a.UserName)}", a.LastMod, "weekly", "0.7");
        EndUrlset(sb);
        return sb.ToString();
    }

    public static async Task<string> BuildTaxonomiesAsync(ApplicationDbContext db, string baseUrl, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var categories = await db.Categories.AsNoTracking()
            .Where(c => c.Posts.Any(p =>
                p.IsPublished && !p.IsDeleted
                && (p.ExpiresAtUtc == null || p.ExpiresAtUtc > now)))
            .Select(c => new
            {
                c.Slug,
                LastMod = c.Posts
                    .Where(p => p.IsPublished && !p.IsDeleted)
                    .Select(p => (DateTime?)p.UpdatedAtUtc)
                    .Max()
            })
            .ToListAsync(ct);

        var tags = await db.Tags.AsNoTracking()
            .Where(t => t.PostTags.Any(pt =>
                pt.Post.IsPublished && !pt.Post.IsDeleted
                && (pt.Post.ExpiresAtUtc == null || pt.Post.ExpiresAtUtc > now)))
            .Select(t => new
            {
                t.Slug,
                LastMod = t.PostTags
                    .Where(pt => pt.Post.IsPublished && !pt.Post.IsDeleted)
                    .Select(pt => (DateTime?)pt.Post.UpdatedAtUtc)
                    .Max()
            })
            .ToListAsync(ct);

        var series = await db.PostSeries.AsNoTracking()
            .Where(s => s.Posts.Any(sp =>
                sp.Post.IsPublished && !sp.Post.IsDeleted
                && (sp.Post.ExpiresAtUtc == null || sp.Post.ExpiresAtUtc > now)))
            .Select(s => new
            {
                s.Slug,
                LastMod = s.Posts
                    .Where(sp => sp.Post.IsPublished && !sp.Post.IsDeleted)
                    .Select(sp => (DateTime?)sp.Post.UpdatedAtUtc)
                    .Max()
            })
            .ToListAsync(ct);

        var sb = StartUrlset();
        foreach (var c in categories)
            AppendUrl(sb, $"{baseUrl}/?category={Uri.EscapeDataString(c.Slug)}",
                c.LastMod, "weekly", "0.5");
        foreach (var t in tags)
            AppendUrl(sb, $"{baseUrl}/?tag={Uri.EscapeDataString(t.Slug)}",
                t.LastMod, "weekly", "0.4");
        foreach (var s in series)
            AppendUrl(sb, $"{baseUrl}/series/{Uri.EscapeDataString(s.Slug)}",
                s.LastMod, "weekly", "0.5");
        EndUrlset(sb);
        return sb.ToString();
    }

    private static StringBuilder StartUrlset()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\" xmlns:xhtml=\"http://www.w3.org/1999/xhtml\">");
        return sb;
    }

    private static void EndUrlset(StringBuilder sb) => sb.AppendLine("</urlset>");

    private static void AppendUrl(
        StringBuilder sb,
        string loc,
        DateTime? lastmod,
        string freq,
        string priority,
        IEnumerable<(string Lang, string Href)>? alternates = null)
    {
        sb.AppendLine("  <url>");
        sb.AppendLine($"    <loc>{Escape(loc)}</loc>");
        if (lastmod.HasValue)
            sb.AppendLine($"    <lastmod>{FormatLastMod(lastmod.Value)}</lastmod>");
        sb.AppendLine($"    <changefreq>{freq}</changefreq>");
        sb.AppendLine($"    <priority>{priority}</priority>");
        if (alternates != null)
        {
            foreach (var a in alternates)
            {
                sb.AppendLine(
                    $"    <xhtml:link rel=\"alternate\" hreflang=\"{Escape(a.Lang)}\" href=\"{Escape(a.Href)}\" />");
            }
        }
        sb.AppendLine("  </url>");
    }

    private static string Escape(string s) => System.Security.SecurityElement.Escape(s) ?? s;
}
