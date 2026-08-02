using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

[Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
public partial class AdminAnalyticsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IUiTranslator _t;

    public AdminAnalyticsController(ApplicationDbContext db, IUiTranslator t)
    {
        _db = db;
        _t = t;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int range = 30)
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
        var bounceRate = sessions.Count == 0 ? 0 : Math.Round(100.0 * bounced / sessions.Count, 1);

        var durations = await _db.ReadingDurationLogs.AsNoTracking()
            .Where(r => r.LoggedAtUtc >= rangeStart && myPostIds.Contains(r.PostId))
            .Select(r => r.DurationSeconds)
            .ToListAsync();
        var avgRead = durations.Count == 0 ? 0 : (int)durations.Average();

        var unique = views.Select(v => v.VisitorHash).Where(h => !string.IsNullOrEmpty(h)).Distinct().Count();
        var viewsPerVisitor = unique == 0 ? 0 : Math.Round((double)views.Count / unique, 2);

        var returning = 0;
        if (unique > 0)
        {
            returning = await _db.PostViews.AsNoTracking()
                .Where(v => v.ViewedAtUtc < rangeStart && myPostIds.Contains(v.PostId))
                .Select(v => v.VisitorHash)
                .Distinct()
                .CountAsync();
        }
        var returningPct = unique == 0 ? 0 : Math.Round(100.0 * Math.Min(returning, unique) / unique, 1);

        var popular = await postQuery.AsNoTracking()
            .OrderByDescending(p => p.ViewCount)
            .Take(8)
            .Select(p => new TopPostItem { Title = p.Title, Slug = p.Slug, Views = p.ViewCount, RangeViews = 0 })
            .ToListAsync();

        var rangeByPost = views.GroupBy(v => v.PostId).ToDictionary(g => g.Key, g => g.Count());
        var trendingPosts = new List<TopPostItem>();
        foreach (var g in views.GroupBy(v => v.PostId).OrderByDescending(g => g.Count()).Take(8))
        {
            var p = await _db.Posts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == g.Key);
            if (p is null) continue;
            trendingPosts.Add(MakeTop(p.Title, p.Slug, p.ViewCount, g.Count()));
        }

        var searchLogs = await _db.SearchQueryLogs.AsNoTracking()
            .Where(s => s.SearchedAtUtc >= rangeStart)
            .ToListAsync();
        var searchKw = searchLogs
            .GroupBy(s => s.Query.Trim().ToLowerInvariant())
            .Select(g => new NamedCount { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(15)
            .ToList();

        var zeroLogs = searchLogs.Where(s => s.ResultCount <= 0).ToList();
        var zeroKw = zeroLogs
            .GroupBy(s => s.Query.Trim().ToLowerInvariant())
            .Select(g => new NamedCount { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(12)
            .ToList();
        var zeroRate = searchLogs.Count == 0
            ? 0
            : Math.Round(zeroLogs.Count * 100.0 / searchLogs.Count, 1);

        var heatmapClicks = myPostIds.Count == 0
            ? 0
            : await _db.HeatmapClicks.AsNoTracking()
                .CountAsync(h => h.ClickedAtUtc >= rangeStart && myPostIds.Contains(h.PostId));

        ApiAnalyticsPanel? apiPanel = null;
        if (AuthorAccess.IsSuperAdmin(User))
        {
            try { apiPanel = await BuildApiPanelAsync(range); }
            catch { }
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
            ZeroResultKeywords = zeroKw,
            ZeroResultSearchCount = zeroLogs.Count,
            ZeroResultRatePercent = zeroRate,
            PopularPosts = popular,
            TrendingPosts = trendingPosts,
            Api = apiPanel
        });
    }

    private static TopPostItem MakeTop(string title, string slug, int views, int rangeViews) => new()
    {
        Title = title,
        Slug = slug,
        Views = views,
        RangeViews = rangeViews
    };

    [HttpGet]
    public IActionResult Heatmaps()
    {
        ViewData["Title"] = _t["ana.heatmap_list_title"];
        return View();
    }
}
