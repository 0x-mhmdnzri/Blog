using System.Text.RegularExpressions;
using BlogApp.Data;
using BlogApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Services;

/// <summary>
/// Scans published post Markdown for internal links that no longer resolve to a
/// published (non-deleted) post, category filter, or known static path.
/// </summary>
public class BrokenLinkService
{
    // Markdown links: [text](url) and bare URLs that look internal
    private static readonly Regex MdLinkRegex = new(
        @"\[([^\]]*)\]\(([^)]+)\)",
        RegexOptions.Compiled);

    private static readonly HashSet<string> KnownStaticPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/", "/robots.txt", "/sitemap.xml"
    };

    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;

    public BrokenLinkService(ApplicationDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<int> ScanAsync(CancellationToken ct = default)
    {
        var baseUrl = (_config["Seo:BaseUrl"] ?? "").TrimEnd('/');
        var posts = await _db.Posts
            .Where(p => p.IsPublished && !p.IsDeleted)
            .Select(p => new { p.Id, p.Title, p.Slug, p.ContentMarkdown })
            .ToListAsync(ct);

        var liveSlugs = posts.Select(p => p.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var categorySlugs = await _db.Categories.Select(c => c.Slug).ToListAsync(ct);
        var catSet = categorySlugs.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var found = new List<BrokenLinkReport>();

        foreach (var post in posts)
        {
            foreach (Match m in MdLinkRegex.Matches(post.ContentMarkdown ?? ""))
            {
                var href = m.Groups[2].Value.Trim();
                if (string.IsNullOrWhiteSpace(href)) continue;
                if (href.StartsWith("#", StringComparison.Ordinal)) continue; // fragment only
                if (href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) continue;
                if (href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)) continue;

                var path = NormalizeInternalPath(href, baseUrl);
                if (path is null) continue; // external — skip

                if (KnownStaticPaths.Contains(path)) continue;
                if (path.StartsWith("/media/", StringComparison.OrdinalIgnoreCase)) continue;
                if (path.StartsWith("/author/", StringComparison.OrdinalIgnoreCase)) continue;

                // /post/{slug}
                if (path.StartsWith("/post/", StringComparison.OrdinalIgnoreCase))
                {
                    var slug = path["/post/".Length..].Trim('/');
                    if (string.IsNullOrEmpty(slug) || liveSlugs.Contains(slug)) continue;
                    found.Add(MakeReport(post.Id, post.Title, post.Slug, href, path));
                    continue;
                }

                // /?category=slug or /?tag=slug — treat missing category as broken
                if (path.StartsWith("/?", StringComparison.Ordinal) || path == "/")
                {
                    // only flag explicit category= that does not exist
                    var q = path.Contains('?') ? path[(path.IndexOf('?') + 1)..] : "";
                    foreach (var part in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var kv = part.Split('=', 2);
                        if (kv.Length == 2 && kv[0].Equals("category", StringComparison.OrdinalIgnoreCase)
                            && !catSet.Contains(Uri.UnescapeDataString(kv[1])))
                        {
                            found.Add(MakeReport(post.Id, post.Title, post.Slug, href, path));
                        }
                    }
                    continue;
                }

                // Unknown internal path that is not home
                if (path != "/" && !path.StartsWith("/Account", StringComparison.OrdinalIgnoreCase)
                    && !path.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase)
                    && !path.StartsWith("/Posts", StringComparison.OrdinalIgnoreCase))
                {
                    found.Add(MakeReport(post.Id, post.Title, post.Slug, href, path));
                }
            }
        }

        // Replace previous scan results
        var old = await _db.BrokenLinkReports.ToListAsync(ct);
        _db.BrokenLinkReports.RemoveRange(old);
        _db.BrokenLinkReports.AddRange(found);
        await _db.SaveChangesAsync(ct);
        return found.Count;
    }

    private static BrokenLinkReport MakeReport(int postId, string title, string slug, string url, string path) =>
        new()
        {
            PostId = postId,
            PostTitle = title,
            PostSlug = slug,
            Url = url,
            NormalizedPath = path,
            DetectedAtUtc = DateTime.UtcNow
        };

    /// <summary>
    /// Returns a site-relative path if the URL is internal; otherwise null.
    /// </summary>
    public static string? NormalizeInternalPath(string href, string configuredBaseUrl)
    {
        href = href.Trim();
        if (href.StartsWith("//", StringComparison.Ordinal)) return null;

        if (href.StartsWith("/", StringComparison.Ordinal))
        {
            // relative path — strip query for post matching but keep for category
            var pathOnly = href.Split('#')[0];
            return pathOnly.Length == 0 ? "/" : pathOnly;
        }

        if (!Uri.TryCreate(href, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme is not ("http" or "https")) return null;

        if (!string.IsNullOrEmpty(configuredBaseUrl)
            && Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var baseUri)
            && string.Equals(uri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase))
        {
            return uri.PathAndQuery.Split('#')[0];
        }

        return null;
    }
}
