using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BlogApp.Data;
using BlogApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Services;

public sealed class MigrationImportResult
{
    public int PostsCreated { get; set; }
    public int PostsSkipped { get; set; }
    public int RedirectsCreated { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public interface IMigrationImportService
{
    Task<MigrationImportResult> ImportWordPressWxrAsync(Stream wxrStream, string authorUserId, string languageCode, bool createRedirects, bool publishImmediately, CancellationToken ct = default);
    Task<MigrationImportResult> ImportGhostJsonAsync(Stream jsonStream, string authorUserId, string languageCode, bool createRedirects, bool publishImmediately, CancellationToken ct = default);
}

/// <summary>
/// WordPress WXR + Ghost JSON importer with optional auto-301 redirect rules.
/// FEATURES.md SEO: Migration importer.
/// </summary>
public sealed class MigrationImportService : IMigrationImportService
{
    private static readonly XNamespace Wp = "http://wordpress.org/export/1.2/";
    private static readonly XNamespace Content = "http://purl.org/rss/1.0/modules/content/";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";

    private readonly ApplicationDbContext _db;
    private readonly MarkdownService _markdown;
    private readonly ILogger<MigrationImportService> _log;

    public MigrationImportService(
        ApplicationDbContext db,
        MarkdownService markdown,
        ILogger<MigrationImportService> log)
    {
        _db = db;
        _markdown = markdown;
        _log = log;
    }

    public async Task<MigrationImportResult> ImportWordPressWxrAsync(
        Stream wxrStream,
        string authorUserId,
        string languageCode,
        bool createRedirects,
        bool publishImmediately,
        CancellationToken ct = default)
    {
        var result = new MigrationImportResult();
        var lang = AppCultures.Normalize(languageCode);

        XDocument doc;
        try
        {
            doc = await XDocument.LoadAsync(wxrStream, LoadOptions.None, ct);
        }
        catch (Exception ex)
        {
            result.Warnings.Add("Invalid WXR XML: " + ex.Message);
            return result;
        }

        var items = doc.Descendants("item").ToList();
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            var postType = (string?)item.Element(Wp + "post_type") ?? "";
            if (!string.Equals(postType, "post", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(postType, "page", StringComparison.OrdinalIgnoreCase))
            {
                result.PostsSkipped++;
                continue;
            }

            var status = ((string?)item.Element(Wp + "status") ?? "").Trim().ToLowerInvariant();
            if (status is "trash" or "auto-draft")
            {
                result.PostsSkipped++;
                continue;
            }

            var title = ((string?)item.Element("title") ?? "Untitled").Trim();
            if (title.Length > 200) title = title[..200];

            var slugRaw = ((string?)item.Element(Wp + "post_name") ?? "").Trim();
            if (string.IsNullOrEmpty(slugRaw))
                slugRaw = Services.SlugHelper.Slugify(title);
            else
                slugRaw = Services.SlugHelper.Slugify(Uri.UnescapeDataString(slugRaw));

            var content = ((string?)item.Element(Content + "encoded") ?? "").Trim();
            var markdown = HtmlToRoughMarkdown(content);

            var link = ((string?)item.Element("link") ?? "").Trim();
            var dateGmt = ((string?)item.Element(Wp + "post_date_gmt") ?? "").Trim();
            DateTime? publishedAt = null;
            if (DateTime.TryParse(dateGmt, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt)
                && dt.Year > 1971)
                publishedAt = dt;

            var isPub = publishImmediately && status is "publish" or "published";

            var created = await UpsertPostAsync(
                title, slugRaw, markdown, authorUserId, lang, isPub, publishedAt, ct);
            if (!created.Created)
            {
                result.PostsSkipped++;
                result.Warnings.Add($"Skip existing slug: {created.Slug}");
                continue;
            }

            result.PostsCreated++;

            if (createRedirects && !string.IsNullOrEmpty(link)
                && Uri.TryCreate(link, UriKind.Absolute, out var oldUri))
            {
                var from = NormalizePath(oldUri.AbsolutePath);
                var to = $"/{lang}/post/{created.Slug}";
                if (await EnsureRedirectAsync(from, to, $"wp-import:{created.Slug}", ct))
                    result.RedirectsCreated++;
            }
        }

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("WXR import created={C} skipped={S} redirects={R}",
            result.PostsCreated, result.PostsSkipped, result.RedirectsCreated);
        return result;
    }

    public async Task<MigrationImportResult> ImportGhostJsonAsync(
        Stream jsonStream,
        string authorUserId,
        string languageCode,
        bool createRedirects,
        bool publishImmediately,
        CancellationToken ct = default)
    {
        var result = new MigrationImportResult();
        var lang = AppCultures.Normalize(languageCode);

        JsonDocument doc;
        try
        {
            doc = await JsonDocument.ParseAsync(jsonStream, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            result.Warnings.Add("Invalid Ghost JSON: " + ex.Message);
            return result;
        }

        using (doc)
        {
            var postsEl = FindPostsArray(doc.RootElement);
            if (postsEl is null || postsEl.Value.ValueKind != JsonValueKind.Array)
            {
                result.Warnings.Add("No posts array found in Ghost JSON.");
                return result;
            }

            foreach (var p in postsEl.Value.EnumerateArray())
            {
                ct.ThrowIfCancellationRequested();
                var type = GetStr(p, "type") ?? "post";
                if (!string.Equals(type, "post", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(type, "page", StringComparison.OrdinalIgnoreCase))
                {
                    result.PostsSkipped++;
                    continue;
                }

                var status = (GetStr(p, "status") ?? "").ToLowerInvariant();

                var title = (GetStr(p, "title") ?? "Untitled").Trim();
                if (title.Length > 200) title = title[..200];

                var slugRaw = GetStr(p, "slug") ?? Services.SlugHelper.Slugify(title);
                slugRaw = Services.SlugHelper.Slugify(slugRaw);

                var markdown = GetStr(p, "markdown")
                               ?? GetStr(p, "mobiledoc")
                               ?? "";
                if (string.IsNullOrWhiteSpace(markdown))
                {
                    var html = GetStr(p, "html") ?? "";
                    markdown = HtmlToRoughMarkdown(html);
                }

                DateTime? publishedAt = null;
                var pubStr = GetStr(p, "published_at") ?? GetStr(p, "published_at_tz");
                if (!string.IsNullOrEmpty(pubStr)
                    && DateTime.TryParse(pubStr, CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out var pdt))
                    publishedAt = pdt.ToUniversalTime();

                var isPub = publishImmediately && status is "published" or "publish";

                var created = await UpsertPostAsync(
                    title, slugRaw, markdown, authorUserId, lang, isPub, publishedAt, ct);
                if (!created.Created)
                {
                    result.PostsSkipped++;
                    continue;
                }

                result.PostsCreated++;

                if (createRedirects)
                {
                    var url = GetStr(p, "url") ?? GetStr(p, "canonical_url");
                    if (!string.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out var oldUri))
                    {
                        var from = NormalizePath(oldUri.AbsolutePath);
                        var to = $"/{lang}/post/{created.Slug}";
                        if (await EnsureRedirectAsync(from, to, $"ghost-import:{created.Slug}", ct))
                            result.RedirectsCreated++;
                    }
                    else
                    {
                        var from = NormalizePath("/" + slugRaw + "/");
                        var to = $"/{lang}/post/{created.Slug}";
                        if (from != to && await EnsureRedirectAsync(from, to, $"ghost-import:{created.Slug}", ct))
                            result.RedirectsCreated++;
                    }
                }
            }
        }

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Ghost import created={C} skipped={S} redirects={R}",
            result.PostsCreated, result.PostsSkipped, result.RedirectsCreated);
        return result;
    }

    private async Task<(bool Created, string Slug)> UpsertPostAsync(
        string title,
        string slugBase,
        string markdown,
        string authorUserId,
        string lang,
        bool isPublished,
        DateTime? publishedAt,
        CancellationToken ct)
    {
        if (await _db.Posts.AnyAsync(p => p.Slug == slugBase && p.LanguageCode == lang && !p.IsDeleted, ct))
            return (false, slugBase);

        var slug = await MakeUniqueSlugAsync(slugBase, lang, ct);

        var now = DateTime.UtcNow;
        var post = new Post
        {
            Title = title,
            Slug = slug,
            Summary = Truncate(StripPlain(markdown), 280),
            ContentMarkdown = markdown,
            AuthorId = authorUserId,
            IsPublished = isPublished,
            PublishedAtUtc = isPublished ? (publishedAt ?? now) : null,
            LanguageCode = lang,
            TranslationStatus = TranslationStatus.Original,
            ReadingTimeMinutes = _markdown.EstimateReadingTimeMinutes(markdown),
            CreatedAtUtc = publishedAt ?? now,
            UpdatedAtUtc = now
        };
        _db.Posts.Add(post);
        await _db.SaveChangesAsync(ct);
        post.TranslationGroupId = post.Id;
        await _db.SaveChangesAsync(ct);
        return (true, slug);
    }

    private async Task<bool> EnsureRedirectAsync(string fromPath, string toUrl, string notes, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(fromPath) || fromPath == "/" || fromPath == toUrl)
            return false;

        var existing = await _db.RedirectRules.FirstOrDefaultAsync(r => r.FromPath == fromPath, ct);
        if (existing is not null)
        {
            existing.ToUrl = toUrl;
            existing.StatusCode = 301;
            existing.IsActive = true;
            existing.Notes = notes;
            return false;
        }

        _db.RedirectRules.Add(new RedirectRule
        {
            FromPath = fromPath,
            ToUrl = toUrl,
            StatusCode = 301,
            IsActive = true,
            Notes = notes,
            CreatedAtUtc = DateTime.UtcNow
        });
        return true;
    }

    private async Task<string> MakeUniqueSlugAsync(string baseSlug, string languageCode, CancellationToken ct)
    {
        var slug = baseSlug;
        var n = 0;
        while (await _db.Posts.AnyAsync(p => p.Slug == slug && p.LanguageCode == languageCode && !p.IsDeleted, ct))
        {
            n++;
            slug = $"{baseSlug}-{n}";
        }
        return slug;
    }

    private static JsonElement? FindPostsArray(JsonElement root)
    {
        if (root.TryGetProperty("posts", out var posts))
            return posts;

        if (root.TryGetProperty("db", out var db) && db.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in db.EnumerateArray())
            {
                if (entry.TryGetProperty("data", out var data)
                    && data.TryGetProperty("posts", out var p2))
                    return p2;
            }
        }

        if (root.TryGetProperty("data", out var data2)
            && data2.TryGetProperty("posts", out var p3))
            return p3;

        return null;
    }

    private static string? GetStr(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p)) return null;
        return p.ValueKind switch
        {
            JsonValueKind.String => p.GetString(),
            JsonValueKind.Number => p.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "/";
        var p = path.Trim();
        if (!p.StartsWith('/')) p = "/" + p;
        var q = p.IndexOf('?');
        if (q >= 0) p = p[..q];
        var h = p.IndexOf('#');
        if (h >= 0) p = p[..h];
        while (p.Length > 1 && p.EndsWith('/')) p = p[..^1];
        return p;
    }

    private static string HtmlToRoughMarkdown(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var s = html;
        s = Regex.Replace(s, "(?is)<script[^>]*>.*?</script>", "");
        s = Regex.Replace(s, "(?is)<style[^>]*>.*?</style>", "");
        s = Regex.Replace(s, "(?i)<h1[^>]*>(.*?)</h1>", "\n# $1\n");
        s = Regex.Replace(s, "(?i)<h2[^>]*>(.*?)</h2>", "\n## $1\n");
        s = Regex.Replace(s, "(?i)<h3[^>]*>(.*?)</h3>", "\n### $1\n");
        s = Regex.Replace(s, "(?i)<strong[^>]*>(.*?)</strong>", "**$1**");
        s = Regex.Replace(s, "(?i)<b[^>]*>(.*?)</b>", "**$1**");
        s = Regex.Replace(s, "(?i)<em[^>]*>(.*?)</em>", "_$1_");
        s = Regex.Replace(s, "(?i)<i[^>]*>(.*?)</i>", "_$1_");
        s = Regex.Replace(s, "(?i)<code[^>]*>(.*?)</code>", "`$1`");
        // Match href='...' or href="..." without breaking C# string literals
        s = Regex.Replace(s, "(?i)<a[^>]+href=['\"]([^'\"]+)['\"][^>]*>(.*?)</a>", "[$2]($1)");
        s = Regex.Replace(s, "(?i)<img[^>]+src=['\"]([^'\"]+)['\"][^>]*/?>", "![]($1)");
        s = Regex.Replace(s, "(?i)<br\\s*/?>", "\n");
        s = Regex.Replace(s, "(?i)</p>", "\n\n");
        s = Regex.Replace(s, "(?i)<li[^>]*>(.*?)</li>", "- $1\n");
        s = Regex.Replace(s, "(?i)<[^>]+>", "");
        s = System.Net.WebUtility.HtmlDecode(s);
        s = Regex.Replace(s, "\n{3,}", "\n\n");
        return s.Trim();
    }

    private static string StripPlain(string md)
    {
        var s = Regex.Replace(md, "[#>*`\\[\\]()_~-]+", " ");
        return Regex.Replace(s, "\\s+", " ").Trim();
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
        return s[..(max - 1)].TrimEnd() + "…";
    }
}
