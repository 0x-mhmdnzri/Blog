using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

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

        var postQuery = _db.Posts.AsQueryable();
        if (!seeAll) postQuery = postQuery.Where(p => p.AuthorId == userId);
        var myPostIds = await postQuery.Select(p => p.Id).ToListAsync();

        var views = await _db.PostViews.AsNoTracking()
            .Where(v => v.ViewedAtUtc >= rangeStart && myPostIds.Contains(v.PostId))
            .ToListAsync();

        var viewsByDay = new List<ChartPoint>();
        for (var d = rangeStart; d <= today; d = d.AddDays(1))
        {
            viewsByDay.Add(new ChartPoint
            {
                Label = d.ToString("MM-dd"),
                Value = views.Count(v => v.ViewedAtUtc.Date == d)
            });
        }

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

        var popular = await postQuery.AsNoTracking()
            .OrderByDescending(p => p.ViewCount)
            .Take(8)
            .Select(p => new TopPostItem
            {
                Title = p.Title,
                Slug = p.Slug,
                Views = p.ViewCount,
                RangeViews = views.Count(v => v.PostId == p.Id)
            })
            .ToListAsync();

        var searchKw = await _db.SearchQueryLogs.AsNoTracking()
            .Where(s => s.SearchedAtUtc >= rangeStart)
            .GroupBy(s => s.Query.ToLower())
            .Select(g => new NamedCount { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(15)
            .ToListAsync();

        var heatmapOptions = await postQuery.AsNoTracking()
            .OrderByDescending(p => p.ViewCount)
            .Take(30)
            .Select(p => new ValueTuple<int, string>(p.Id, p.Title))
            .ToListAsync();

        var hmId = heatmapPostId ?? heatmapOptions.FirstOrDefault().Item1;
        List<HeatmapPoint> heatmap = new();
        string? hmTitle = null;
        if (hmId > 0)
        {
            hmTitle = heatmapOptions.FirstOrDefault(o => o.Item1 == hmId).Item2;
            var clicks = await _db.HeatmapClicks.AsNoTracking()
                .Where(h => h.PostId == hmId && h.ClickedAtUtc >= rangeStart)
                .ToListAsync();
            // Bucket 50×50 grid (0–1000 → 0–20)
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

        ViewData["Title"] = "آمار و تحلیل";
        return View(new AnalyticsDashboardViewModel
        {
            RangeDays = range,
            TotalViews = views.Count,
            UniqueVisitors = views.Select(v => v.VisitorHash).Distinct().Count(),
            BounceRatePercent = bounceRate,
            AvgReadingSeconds = avgRead,
            ViewsByDay = viewsByDay,
            TrafficSources = Group(views.Select(v => v.TrafficSource)),
            Devices = Group(views.Select(v => v.DeviceType)),
            Browsers = Group(views.Select(v => v.Browser)),
            Countries = Group(views.Select(v => v.CountryCode)),
            Referrers = Group(views.Select(v => v.ReferrerHost)),
            SearchKeywords = searchKw,
            PopularPosts = popular,
            TrendingPosts = trendingPosts,
            Heatmap = heatmap,
            HeatmapPostId = hmId > 0 ? hmId : null,
            HeatmapPostTitle = hmTitle,
            HeatmapPostOptions = heatmapOptions
        });
    }
}
