using BlogApp.Data;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Services.Seo;

public sealed class DeepPostItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string LanguageCode { get; set; } = "fa";
    public int Depth { get; set; }
    public int ViewCount { get; set; }
    public string? CategoryName { get; set; }
    public string Path => $"/{LanguageCode}/post/{Slug}";
}

public sealed class ClickDepthReport
{
    public int PublishedCount { get; set; }
    public int ReachableCount { get; set; }
    public int Beyond4Count { get; set; }
    public int UnreachableCount { get; set; }
    public int MaxDepth { get; set; }
    public Dictionary<int, int> DepthHistogram { get; set; } = new();
    public List<DeepPostItem> Beyond4 { get; set; } = new();
    public List<DeepPostItem> Unreachable { get; set; } = new();
}

/// <summary>
/// P2.1 — approximate internal click depth from homepage.
/// Edges: home feed + featured/sticky, category hubs, series hubs, related (same category / series).
/// Target: priority content ≤ 4 clicks.
/// </summary>
public static class ClickDepthAnalyzer
{
    public const int PageSize = 8;
    public const int MaxRelatedEdges = 6;
    public const int DepthLimit = 8;

    public static async Task<ClickDepthReport> BuildAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var posts = await db.Posts.AsNoTracking()
            .Where(p => p.IsPublished && !p.IsDeleted)
            .Where(p => p.ExpiresAtUtc == null || p.ExpiresAtUtc > now)
            .Select(p => new
            {
                p.Id,
                p.Title,
                p.Slug,
                p.LanguageCode,
                p.ViewCount,
                p.CategoryId,
                CategoryName = p.Category != null ? p.Category.Name : null,
                p.IsFeatured,
                p.IsSticky,
                p.PublishedAtUtc,
                p.UpdatedAtUtc
            })
            .ToListAsync(ct);

        if (posts.Count == 0)
            return new ClickDepthReport();

        var postIds = posts.Select(p => p.Id).ToList();
        var seriesLinks = await db.SeriesPosts.AsNoTracking()
            .Where(sp => postIds.Contains(sp.PostId))
            .Select(sp => new { sp.SeriesId, sp.PostId })
            .ToListAsync(ct);

        var seriesByPost = seriesLinks.GroupBy(x => x.PostId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.SeriesId).ToList());
        var postsBySeries = seriesLinks.GroupBy(x => x.SeriesId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.PostId).ToList());

        var byId = posts.ToDictionary(p => p.Id);
        var depth = new Dictionary<int, int>();
        var q = new Queue<int>();

        void Touch(int id, int d)
        {
            if (d > DepthLimit) return;
            if (depth.TryGetValue(id, out var existing) && existing <= d) return;
            depth[id] = d;
            q.Enqueue(id);
        }

        foreach (var p in posts
            .OrderByDescending(p => p.IsSticky)
            .ThenByDescending(p => p.IsFeatured)
            .ThenByDescending(p => p.PublishedAtUtc ?? p.UpdatedAtUtc)
            .Take(PageSize))
            Touch(p.Id, 1);

        foreach (var p in posts.Where(x => x.IsFeatured || x.IsSticky))
            Touch(p.Id, 1);

        foreach (var catGroup in posts.Where(p => p.CategoryId != null).GroupBy(p => p.CategoryId!.Value))
        {
            foreach (var p in catGroup
                .OrderByDescending(x => x.PublishedAtUtc ?? x.UpdatedAtUtc)
                .Take(PageSize))
                Touch(p.Id, 2);
        }

        foreach (var kv in postsBySeries)
        {
            foreach (var pid in kv.Value.Take(PageSize * 2))
                Touch(pid, 2);
        }

        var byCategory = posts.Where(p => p.CategoryId != null)
            .GroupBy(p => p.CategoryId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(p => p.PublishedAtUtc ?? p.UpdatedAtUtc)
                      .Select(p => p.Id).ToList());

        while (q.Count > 0)
        {
            var id = q.Dequeue();
            var d = depth[id];
            if (d >= DepthLimit) continue;
            if (!byId.TryGetValue(id, out var node)) continue;

            if (node.CategoryId is int cid && byCategory.TryGetValue(cid, out var peers))
            {
                var n = 0;
                foreach (var peerId in peers)
                {
                    if (peerId == id) continue;
                    Touch(peerId, d + 1);
                    if (++n >= MaxRelatedEdges) break;
                }
            }

            if (seriesByPost.TryGetValue(id, out var sids))
            {
                foreach (var sid in sids)
                {
                    if (!postsBySeries.TryGetValue(sid, out var members)) continue;
                    var n = 0;
                    foreach (var mid in members)
                    {
                        if (mid == id) continue;
                        Touch(mid, d + 1);
                        if (++n >= MaxRelatedEdges) break;
                    }
                }
            }
        }

        var report = new ClickDepthReport
        {
            PublishedCount = posts.Count,
            ReachableCount = depth.Count,
            MaxDepth = depth.Count > 0 ? depth.Values.Max() : 0
        };

        for (var i = 1; i <= Math.Max(report.MaxDepth, 4); i++)
            report.DepthHistogram[i] = depth.Values.Count(v => v == i);

        report.Beyond4 = posts
            .Where(p => depth.TryGetValue(p.Id, out var d) && d > 4)
            .Select(p => new DeepPostItem
            {
                Id = p.Id,
                Title = p.Title,
                Slug = p.Slug,
                LanguageCode = p.LanguageCode,
                Depth = depth[p.Id],
                ViewCount = p.ViewCount,
                CategoryName = p.CategoryName
            })
            .OrderByDescending(x => x.Depth)
            .ThenBy(x => x.ViewCount)
            .Take(50)
            .ToList();

        report.Unreachable = posts
            .Where(p => !depth.ContainsKey(p.Id))
            .Select(p => new DeepPostItem
            {
                Id = p.Id,
                Title = p.Title,
                hardSlug = p.Slug,
                Slug = p.Slug,
                LanguageCode = p.LanguageCode,
                Depth = -1,
                ViewCount = p.ViewCount,
                CategoryName = p.CategoryName
            })
            .OrderBy(x => x.ViewCount)
            .Take(50)
            .ToList();

        // strip invalid hardSlug property if present in source — rebuild list
        report.Unreachable = report.Unreachable.Select(x => new DeepPostItem
        {
            Id = x.Id,
            Title = x.Title,
            Slug = x.Slug,
            LanguageCode = x.LanguageCode,
            Depth = -1,
            ViewCount = x.ViewCount,
            CategoryName = x.CategoryName
        }).ToList();

        report.Beyond4Count = report.Beyond4.Count;
        report.UnreachableCount = posts.Count(p => !depth.ContainsKey(p.Id));
        return report;
    }
}
