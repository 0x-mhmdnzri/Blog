using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

/// <summary>
/// Deep visitor analytics (traffic, devices, geo, search, heatmap, engagement).
/// Operational CMS metrics stay on Admin/Index dashboard.
/// </summary>
[Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
public class AdminAnalyticsController : Controller
{
    private readonly ApplicationDbContext _db;

    public AdminAnalyticsController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index(int range = 30, int? heatmapPostId = null)
    {
        if (range is not (7 or 30 or 90)) range = 30;

        var userId = AuthorAccess.UserId(User)!;
        var seeAll = AuthorAccess.CanViewAllAnalytics(User);
        var today = DateTime.UtcNow.Date;
        var rangeStart = today.AddDays(-(range - 1));

        var postQuery = _db.Posts.AsQueryable().Where(p => !p.IsDeleted);
        if (!seeAll) postQuery = postQuery.Where(p => p.AuthorId == userId);
        var myPostIds = await postQuery.Select(p => p.Id).ToListAsync();

        var views = await _db.PostViews.AsNoTracking()
            .Where(v => v.ViewedAtUtc >= rangeStart && myPostIds.Contains(v.PostId))
            .ToListAsync();

        var rangeViewsByPost = views
            .GroupBy(v => v.PostId)
            .ToDictionary(g => g.Key, g => g.Count());

        var viewsByDay = new List<ChartPoint>();
        for (var d = rangeStart; d <= today; d = d.AddDays(1))
        {
            viewsByDay.Add(new ChartPoint
            {
                Label = d.ToString("MM-dd"),
                Value = views.Count(v => v.ViewedAtUtc.Date == d)
            });
        }

        var viewsByHour = Enumerable.Range(0, 24)
            .Select(h => new ChartPoint
            {
                Label = h.ToString("00"),
                Value = views.Count(v => v.ViewedAtUtc.Hour == h)
            })
            .ToList();

        static List<NamedCount> Group(IEnumerable<string?> items) =>
            items.Where(s => !string.IsNullOrWhiteSpace(s))
                .GroupBy(s => s!)
                .Select(g => new NamedCount { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(12)
                .ToList();

        var sessions = await _db.AnalyticsSessions.AsNoTracking()
            .Where(s => s.StartedAtUtc >= rangeStart)
            .ToListAsync();
        var bounced = sessions.Count(s => s.PageViewCount <= 1);
        var bounceRate = sessions.Count == 0 ? 0 : Math.Round(bounced * 100.0 / sessions.Count, 1);

        var durations = await _db.ReadingDurationLogs.AsNoTracking()
            .Where(r => r.LoggedAtUtc >= rangeStart && myPostIds.Contains(r.PostId))
            .Select(r => r.DurationSeconds)
            .ToListAsync();
        var avgRead = durations.Count == 0 ? 0 : Math.Round(durations.Average(), 1);

        var unique = views.Select(v => v.VisitorHash).Where(h => !string.IsNullOrEmpty(h)).Distinct().Count();
        var viewsPerVisitor = unique == 0 ? 0 : Math.Round(views.Count * 1.0 / unique, 2);

        var rangeHashes = views.Select(v => v.VisitorHash).Where(h => !string.IsNullOrEmpty(h)).Distinct().ToList();
        var returning = 0;
        if (rangeHashes.Count > 0)
        {
            returning = await _db.PostViews.AsNoTracking()
                .Where(v => v.ViewedAtUtc < rangeStart && myPostIds.Contains(v.PostId) && rangeHashes.Contains(v.VisitorHash))
                .Select(v => v.VisitorHash)
                .Distinct()
                .CountAsync();
        }
        var returningPct = unique == 0 ? 0 : Math.Round(returning * 100.0 / unique, 1);

        var trendingStart = DateTime.UtcNow.AddDays(-3);
        var trendingIds = views
            .Where(v => v.ViewedAtUtc >= trendingStart)
            .GroupBy(v => v.PostId)
            .Select(g => new { PostId = g.Key, C = g.Count() })
            .OrderByDescending(x => x.C)
            .Take(8)
            .ToList();

        var trendingPosts = new List<TopPostItem>();
        foreach (var t in trendingIds)
        {
            var p = await postQuery.AsNoTracking().FirstOrDefaultAsync(x => x.Id == t.PostId);
            if (p is null) continue;
            trendingPosts.Add(new TopPostItem
            {
                Title = p.Title,
                Slug = p.Slug,
                Views = p.ViewCount,
                RangeViews = t.C
            });
        }

        // Load posts first (SQL-only), then attach range view counts in memory
        var popularRows = await postQuery.AsNoTracking()
            .OrderByDescending(p => p.ViewCount)
            .Take(8)
            .Select(p => new { p.Id, p.Title, p.Slug, p.ViewCount })
            .ToListAsync();

        var popular = popularRows.Select(p => new TopPostItem
        {
            Title = p.Title,
            Slug = p.Slug,
            Views = p.ViewCount,
            RangeViews = rangeViewsByPost.GetValueOrDefault(p.Id)
        }).ToList();

        var searchLogs = await _db.SearchQueryLogs.AsNoTracking()
            .Where(s => s.SearchedAtUtc >= rangeStart)
            .ToListAsync();
        var searchKw = searchLogs
            .GroupBy(s => s.Query.Trim().ToLowerInvariant())
            .Select(g => new NamedCount { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(15)
            .ToList();

        var heatmapOptions = await postQuery.AsNoTracking()
            .OrderByDescending(p => p.ViewCount)
            .Take(40)
            .Select(p => new ValueTuple<int, string>(p.Id, p.Title))
            .ToListAsync();
        var heatmapNamed = heatmapOptions.Select(t => (Id: t.Item1, Title: t.Item2)).ToList();

        var hmId = heatmapPostId ?? heatmapNamed.FirstOrDefault().Id;
        List<HeatmapPoint> heatmap = new();
        string? hmTitle = null;
        var heatmapClicks = 0;
        if (hmId > 0)
        {
            hmTitle = heatmapNamed.FirstOrDefault(o => o.Id == hmId).Title;
            var clicks = await _db.HeatmapClicks.AsNoTracking()
                .Where(h => h.PostId == hmId && h.ClickedAtUtc >= rangeStart)
                .ToListAsync();
            heatmapClicks = clicks.Count;
            heatmap = clicks
                .GroupBy(c => (c.X / 50) * 50 + "," + (c.Y / 50) * 50)
                .Select(g =>
                {
                    var parts = g.Key.Split(',');
                    return new HeatmapPoint
                    {
                        X = int.Parse(parts[0]),
                        Y = int.Parse(parts[1]),
                        Count = g.Count()
                    };
                })
                .ToList();
        }

        ViewData["Title"] = "Analytics";
        return View(new AnalyticsDashboardViewModel
        {
            RangeDays = range,
            TotalViews = views.Count,
            UniqueVisitors = unique,
            BounceRatePercent = bounceRate,
            AvgReadingSeconds = avgRead,
            ViewsPerVisitor = viewsPerVisitor,
            ReturningVisitorPercent = returningPct,
            SessionCount = sessions.Count,
            HeatmapClickCount = heatmapClicks,
            SearchQueryCount = searchLogs.Count,
            ViewsByDay = viewsByDay,
            ViewsByHour = viewsByHour,
            TrafficSources = Group(views.Select(v => v.TrafficSource)),
            Devices = Group(views.Select(v => v.DeviceType)),
            Browsers = Group(views.Select(v => v.Browser)),
            OperatingSystems = Group(views.Select(v => v.Os)),
            Countries = Group(views.Select(v => v.CountryCode)),
            Referrers = Group(views.Select(v => v.ReferrerHost)),
            SearchKeywords = searchKw,
            PopularPosts = popular,
            TrendingPosts = trendingPosts,
            Heatmap = heatmap,
            HeatmapPostId = hmId > 0 ? hmId : null,
            HeatmapPostTitle = hmTitle,
            HeatmapPostOptions = heatmapNamed
        });
    }
}
