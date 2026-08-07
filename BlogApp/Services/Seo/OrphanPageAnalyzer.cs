using System.Text.RegularExpressions;
using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Services.Seo;

public sealed class OrphanPostItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string LanguageCode { get; set; } = "fa";
    public int ViewCount { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public string? CategoryName { get; set; }
    public string? CategorySlug { get; set; }
    public int ContentInlinks { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsSticky { get; set; }
    public bool InSeries { get; set; }
    public bool InFolder { get; set; }
    public bool IsOrphan { get; set; }
    public string Path => $"/{LanguageCode}/post/{Slug}";
}

public sealed class OrphanPageReport
{
    public int PublishedCount { get; set; }
    public int OrphanCount { get; set; }
    public int WithContentInlinks { get; set; }
    public List<OrphanPostItem> Orphans { get; set; } = new();
    public List<(string FromSlug, string ToSlug)> SampleEdges { get; set; } = new();
}

/// <summary>
/// P1.2 — published posts with zero internal content inlinks and no hub membership
/// (featured / sticky / series / folder). Mainly sitemap-only discovery.
/// </summary>
public static class OrphanPageAnalyzer
{
    private static readonly Regex MdLinkRegex = new(
        @"\[([^\]]*)\]\(([^)]+)\)",
        RegexOptions.Compiled);

    public static async Task<OrphanPageReport> BuildAsync(
        ApplicationDbContext db,
        string? configuredBaseUrl = null,
        CancellationToken ct = default)
    {
        var baseUrl = (configuredBaseUrl ?? "").TrimEnd('/');

        var posts = await db.Posts.AsNoTracking()
            .Where(p => p.IsPublished && !p.IsDeleted)
            .Select(p => new
            {
                p.Id,
                p.Title,
                p.Slug,
                p.LanguageCode,
                p.ViewCount,
                p.PublishedAtUtc,
                p.ContentMarkdown,
                p.IsFeatured,
                p.IsSticky,
                CategoryName = p.Category != null ? p.Category.Name : null,
                CategorySlug = p.Category != null ? p.Category.Slug : null
            })
            .ToListAsync(ct);

        var seriesSet = (await db.SeriesPosts.AsNoTracking()
            .Select(sp => sp.PostId)
            .Distinct()
            .ToListAsync(ct)).ToHashSet();

        var folderSet = (await db.Set<PostFolderItem>().AsNoTracking()
            .Select(i => i.PostId)
            .Distinct()
            .ToListAsync(ct)).ToHashSet();

        var slugToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var pathToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in posts)
        {
            slugToId[p.Slug] = p.Id;
            pathToId[$"/post/{p.Slug}"] = p.Id;
            pathToId[$"/{p.LanguageCode}/post/{p.Slug}"] = p.Id;
        }

        var inlinkSources = new Dictionary<int, HashSet<int>>();
        var edges = new List<(string From, string To)>();
        var idToSlug = posts.ToDictionary(p => p.Id, p => p.Slug);

        foreach (var post in posts)
        {
            var md = post.ContentMarkdown ?? "";
            if (md.Length == 0) continue;

            foreach (Match m in MdLinkRegex.Matches(md))
            {
                var href = m.Groups[2].Value.Trim();
                if (string.IsNullOrWhiteSpace(href) || href.StartsWith('#')) continue;

                var path = BrokenLinkService.NormalizeInternalPath(href, baseUrl);
                if (path is null) continue;

                var pathOnly = path.Split('?', 2)[0].Split('#', 2)[0];
                if (pathOnly.Length > 1 && pathOnly.EndsWith('/'))
                    pathOnly = pathOnly.TrimEnd('/');

                int? targetId = null;
                if (pathToId.TryGetValue(pathOnly, out var byPath))
                    targetId = byPath;
                else
                {
                    var segs = pathOnly.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (segs.Length >= 2
                        && segs[^2].Equals("post", StringComparison.OrdinalIgnoreCase)
                        && slugToId.TryGetValue(segs[^1], out var bySlug))
                        targetId = bySlug;
                }

                if (targetId is null || targetId.Value == post.Id)
                    continue;

                if (!inlinkSources.TryGetValue(targetId.Value, out var set))
                {
                    set = new HashSet<int>();
                    inlinkSources[targetId.Value] = set;
                }

                if (set.Add(post.Id) && edges.Count < 40)
                    edges.Add((post.Slug, idToSlug[targetId.Value]));
            }
        }

        var withInlinks = 0;
        var orphans = new List<OrphanPostItem>();

        foreach (var p in posts)
        {
            var contentIn = inlinkSources.TryGetValue(p.Id, out var src) ? src.Count : 0;
            if (contentIn > 0) withInlinks++;

            var inSeries = seriesSet.Contains(p.Id);
            var inFolder = folderSet.Contains(p.Id);
            var isOrphan = contentIn == 0
                           && !p.IsFeatured
                           && !p.IsSticky
                           && !inSeries
                           && !inFolder;

            if (!isOrphan) continue;

            orphans.Add(new OrphanPostItem
            {
                Id = p.Id,
                Title = p.Title,
                hardSlug = p.Slug,
                hardSlug_REMOVE = null,
                Slug = p.Slug,
                LanguageCode = p.LanguageCode,
                ViewCount = p.ViewCount,
                PublishedAtUtc = p.PublishedAtUtc,
                CategoryName = p.CategoryName,
                CategorySlug = p.CategorySlug,
                ContentInlinks = contentIn,
                IsFeatured = p.IsFeatured,
                IsSticky = p.IsSticky,
                InSeries = inSeries,
                InFolder = inFolder,
                IsOrphan = true
            });
        }

        orphans = orphans
            .OrderBy(o => o.ViewCount)
            .ThenBy(o => o.PublishedAtUtc)
            .Take(100)
            .ToList();

        return new OrphanPageReport
        {
            PublishedCount = posts.Count,
            OrphanCount = orphans.Count,
            WithContentInlinks = withInlinks,
            Orphans = orphans,
            SampleEdges = edges
        };
    }
}
