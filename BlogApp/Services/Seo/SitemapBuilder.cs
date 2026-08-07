using System.Text;
using BlogApp.Data;
using BlogApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Services.Seo;

/// <summary>
/// P1.3 + P3.1 — clean split XML sitemaps with accurate lastmod and dynamic
/// changefreq/priority by content freshness. Google News sitemap for recent posts.
/// </summary>
public static class SitemapBuilder
{
    public const int MaxUrlsPerFile = 40_000; // protocol limit 50k; stay under
    public static readonly TimeSpan NewsWindow = TimeSpan.FromHours(48);

    public static string FormatLastMod(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ");

    public static DateTime BestLastMod(DateTime updatedAt, DateTime? publishedAt) =>
        publishedAt.HasValue && publishedAt.Value > updatedAt ? publishedAt.Value : updatedAt;

    /// <summary>P3.1 — fresher content → higher priority + more frequent changefreq.</summary>
    public static (string Freq, string Priority) CadenceFor(DateTime? lastmodUtc, DateTime now)
    {
        if (lastmodUtc is null)
            return ("monthly", "0.5");

        var age = now - lastmodUtc.Value;
        if (age < TimeSpan.FromHours(24))
            return ("hourly", "1.0");
        if (age < TimeSpan.FromDays(7))
            return ("daily", "0.9");
        if (age < TimeSpan.FromDays(30))
            return ("weekly", "0.8");
        if (age < TimeSpan.FromDays(180))
            return ("monthly", "0.6");
        return ("yearly", "0.4");
    }

    public static Task<string> BuildIndexAsync(string baseUrl)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<sitemapindex xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
        foreach (var name in new[] { "pages", "posts", "authors", "taxonomies", "news" })
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
        var now = DateTime.UtcNow;
        var sb = StartUrlset();
        AppendUrl(sb, $"{baseUrl}/", now, "daily", "1.0");
        foreach (var lang in AppCultures.All.Select(c => c.Code))
            AppendUrl(sb, $"{baseUrl}/{lang}/", now, "daily", "0.9");

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
            var (freq, priority) = CadenceFor(last, now);

            List<(string Lang, string Href)>? alts = null;
            if (byGroup.TryGetValue(post.GroupId, out var siblings) && siblings.Count > 1)
            {
                alts = siblings
                    .Select(s => (s.LanguageCode, $"{baseUrl}/{s.LanguageCode}/post/{s.Slug}"))
                    .ToList();
                var def = siblings.FirstOrDefault(s => s.LanguageCode == "fa") ?? siblings[0];
                alts.Add(("x-default", $"{baseUrl}/{def.LanguageCode}/post/{def.Slug}"));
            }

            AppendUrl(sb, loc, last, freq, priority, alts);
        }
        EndUrlset(sb);
        return sb.ToString();
    }

    /// <summary>
    /// P3.1 — Google News sitemap for posts published within the news window (48h).
    /// Spec: https://developers.google.com/search/docs/crawling-indexing/sitemaps/news-sitemap
    /// </summary>
    public static async Task<string> BuildNewsAsync(ApplicationDbContext db, string baseUrl, string publicationName, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var since = now - NewsWindow;

        var posts = await db.Posts.AsNoTracking()
            .Where(p => p.IsPublished && !p.IsDeleted)
            .Where(p => p.ExpiresAtUtc == null || p.ExpiresAtUtc > now)
            .Where(p => p.ScheduledPublishAtUtc == null || p.ScheduledPublishAtUtc <= now)
            .Where(p => p.TranslationStatus == TranslationStatus.Original
                        || p.TranslationStatus == TranslationStatus.Approved)
            .Where(p => p.PublishedAtUtc != null && p.PublishedAtUtc >= since)
            .OrderByDescending(p => p.PublishedAtUtc)
            .Take(1000)
            .Select(p => new
            {
                p.Slug,
                p.Title,
                p.LanguageCode,
                p.PublishedAtUtc
            })
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\" xmlns:news=\"http://www.google.com/schemas/sitemap-news/0.9\">");
        foreach (var p in posts)
        {
            var loc = $"{baseUrl}/{p.LanguageCode}/post/{p.Slug}";
            var pubDate = (p.PublishedAtUtc ?? now).ToUniversalTime();
            var lang = string.IsNullOrWhiteSpace(p.LanguageCode) ? "fa" : p.LanguageCode;
            sb.AppendLine("  <url>");
            sb.AppendLine($"    <loc>{Escape(loc)}</loc>");
            sb.AppendLine("    <news:news>");
            sb.AppendLine("      <news:publication>");
            sb.AppendLine($"        <news:name>{Escape(publicationName)}</news:name>");
            sb.AppendLine($"        <news:language>{Escape(lang)}</news:language>");
            sb.AppendLine("      </news:publication>");
            sb.AppendLine($"      <news:publication_date>{FormatLastMod(pubDate)}</news:publication_date>");
            sb.AppendLine($"      <news:title>{Escape(p.Title)}</news:title>");
            sb.AppendLine("    </news:news>");
            sb.AppendLine("  </url>");
        }
        sb.AppendLine("</urlset>");
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
        {
            var (freq, priority) = CadenceFor(a.LastMod, now);
            if (priority is "1.0" or "0.9") priority = "0.8";
            AppendUrl(sb, $"{baseUrl}/author/{Uri.EscapeDataString(a.UserName)}", a.LastMod, freq, priority);
        }
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
        {
            var (freq, _) = CadenceFor(c.LastMod, now);
            AppendUrl(sb, $"{baseUrl}/?category={Uri.EscapeDataString(c.Slug)}",
                c.LastMod, freq, "0.5");
        }
        foreach (var t in tags)
        {
            var (freq, _) = CadenceFor(t.LastMod, now);
            AppendUrl(sb, $"{baseUrl}/?tag={Uri.EscapeDataString(t.Slug)}",
                t.LastMod, freq, "0.4");
        }
        foreach (var s in series)
        {
            var (freq, _) = CadenceFor(s.LastMod, now);
            AppendUrl(sb, $"{baseUrl}/series/{Uri.EscapeDataString(s.Slug)}",
                s.LastMod, freq, "0.5");
        }
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
