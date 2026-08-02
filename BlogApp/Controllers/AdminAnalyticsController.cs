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
        var rangeStart = today.AddDays(-range);

        var postQuery = _db.Posts.AsNoTracking().Where(p => !p.IsDeleted);
        if (!seeAll)
            postQuery = postQuery.Where(p => p.AuthorId == userId);

        var myPostIds = await postQuery.Select(p => p.Id).ToListAsync();

        var viewsQ = _db.PostViews.AsNoTracking().Where(v => v.ViewedAtUtc >= rangeStart);
        if (!seeAll)
            viewsQ = viewsQ.Where(v => myPostIds.Contains(v.PostId));

        var views = await viewsQ.ToListAsync();
        var unique = views.Select(v => v.VisitorHash).Distinct().Count();
        var sessions = await _db.AnalyticsSessions.AsNoTracking()
            .Where(s => s.StartedAtUtc >= rangeStart)
            .ToListAsync();

        var bounceRate = sessions.Count == 0
            ? 0
            : Math.Round(sessions.Count(s => s.PageViewCount <= 1) * 100.0 / sessions.Count, 1);

        var readDurations = await _db.ReadingDurations.AsNoTracking()
            .Where(r => r.RecordedAtUtc >= rangeStart && (seeAll || myPostIds.Contains(r.PostId)))
            .Select(r => r.Seconds)
            .ToListAsync();
        var avgRead = readDurations.Count == 0 ? 0 : (int)readDurations.Average();

        var viewsPerVisitor = unique == 0 ? 0 : Math.Round(views.Count * 1.0 / unique, 2);

        var visitorFirst = views
            .GroupBy(v => v.VisitorHash)
            .Select(g => g.Min(x => x.ViewedAtUtc))
            .ToList();
        var returning = visitorFirst.Count(f => f < rangeStart);
        var returningPct = unique == 0 ? 0 : Math.Round(returning * 100.0 / unique, 1);

        var viewsByDay = Enumerable.Range(0, range)
            .Select(i => rangeStart.AddDays(i))
            .Select(d => new NamedCount
            {
                Name = d.ToString("MM-dd"),
                Count = views.Count(v => v.ViewedAtUtc.Date == d)
            })
            .ToList();

        var viewsByHour = Enumerable.Range(0, 24)
            .Select(h => new NamedCount
            {
                Name = h.ToString("00"),
                Count = views.Count(v => v.ViewedAtUtc.Hour == h)
            })
            .ToList();

        static List<NamedCount> Group(IEnumerable<string?> src) =>
            src.Where(s => !string.IsNullOrWhiteSpace(s))
                .GroupBy(s => s!)
                .Select(g => new NamedCount { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(12)
                .ToList();

        var popular = await postQuery
            .OrderByDescending(p => p.ViewCount)
            .Take(10)
            .Select(p => new TopPostItem
            {
                Title = p.Title,
                Slug = p.Slug,
                Views = p.ViewCount,
                RangeViews = 0
            })
            .ToListAsync();

        var rangeViewMap = views.GroupBy(v => v.PostId).ToDictionary(g => g.Key, g => g.Count());
        foreach (var item in popular)
        {
            var post = await postQuery.FirstOrDefaultAsync(p => p.Slug == item.Slug);
            if (post is not null && rangeViewMap.TryGetValue(post.Id, out var rv))
                item.RangeViews = rv;
        }

        var trendingPosts = views
            .GroupBy(v => v.PostId)
            .Select(g => new { PostId = g.Key, C = g.Count() })
            .OrderByDescending(x => x.C)
            .Take(10)
            .ToList();

        var trending = new List<TopPostItem>();
        foreach (var t in trendingPosts)
        {
            var post = await _db.Posts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == t.PostId);
            if (post is null) continue;
            trending.Add(MakeTop(post.Title, post.Slug, post.ViewCount, t.C));
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
            TrendingPosts = trending,
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

    /// <summary>DataTables JSON for posts with heatmap click counts.</summary>
    [HttpGet]
    public async Task<IActionResult> HeatmapsData()
    {
        var req = DataTablesRequest.From(Request);
        var userId = AuthorAccess.UserId(User)!;
        var seeAll = AuthorAccess.CanViewAllAnalytics(User);

        var postsQ = _db.Posts.AsNoTracking().Where(p => !p.IsDeleted);
        if (!seeAll)
            postsQ = postsQ.Where(p => p.AuthorId == userId);

        var clickCounts = await _db.HeatmapClicks.AsNoTracking()
            .GroupBy(h => h.PostId)
            .Select(g => new { PostId = g.Key, Clicks = g.Count() })
            .ToListAsync();
        var clickMap = clickCounts.ToDictionary(x => x.PostId, x => x.Clicks);

        var posts = await postsQ
            .Select(p => new
            {
                p.Id,
                p.Title,
                p.Slug,
                p.ViewCount,
                p.PublishedAtUtc,
                p.CreatedAtUtc,
                p.IsPublished
            })
            .ToListAsync();

        var rowsAll = posts.Select(p => new
        {
            p.Id,
            p.Title,
            p.Slug,
            p.ViewCount,
            Clicks = clickMap.GetValueOrDefault(p.Id, 0),
            Date = p.PublishedAtUtc ?? p.CreatedAtUtc,
            p.IsPublished
        }).ToList();

        var total = rowsAll.Count;

        if (!string.IsNullOrWhiteSpace(req.SearchValue))
        {
            var term = req.SearchValue.Trim();
            rowsAll = rowsAll.Where(p =>
                p.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                || p.Slug.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var filtered = rowsAll.Count;

        rowsAll = (req.OrderColumn, req.Asc) switch
        {
            (1, true) => rowsAll.OrderBy(p => p.Title).ToList(),
            (1, false) => rowsAll.OrderByDescending(p => p.Title).ToList(),
            (2, true) => rowsAll.OrderBy(p => p.ViewCount).ToList(),
            (2, false) => rowsAll.OrderByDescending(p => p.ViewCount).ToList(),
            (3, true) => rowsAll.OrderBy(p => p.Clicks).ToList(),
            (3, false) => rowsAll.OrderByDescending(p => p.Clicks).ToList(),
            (4, true) => rowsAll.OrderBy(p => p.Date).ToList(),
            (4, false) => rowsAll.OrderByDescending(p => p.Date).ToList(),
            _ => rowsAll.OrderByDescending(p => p.Clicks).ThenByDescending(p => p.ViewCount).ToList()
        };

        var page = rowsAll.Skip(req.Start).Take(req.Length).ToList();
        var openLabel = System.Net.WebUtility.HtmlEncode(_t["ana.heatmap_open"]);

        var rows = page.Select((p, i) => new object[]
        {
            req.Start + i + 1,
            System.Net.WebUtility.HtmlEncode(p.Title),
            p.ViewCount,
            p.Clicks,
            PersianDate.Date(p.Date),
            "<a class=\"icon-btn\" href=\"/AdminAnalytics/Heatmap/" + p.Id + "\">" + openLabel + "</a>"
        }).ToList();

        return Json(DataTablesResponse.Ok(req.Draw, total, filtered, rows));
    }

    [HttpGet]
    public async Task<IActionResult> Heatmap(int id, int range = 30)
    {
        if (range is not (0 or 7 or 30 or 90)) range = 30;

        var userId = AuthorAccess.UserId(User)!;
        var seeAll = AuthorAccess.CanViewAllAnalytics(User);

        var post = await _db.Posts.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (post is null) return NotFound();
        if (!seeAll && post.AuthorId != userId) return Forbid();

        var q = _db.HeatmapClicks.AsNoTracking().Where(h => h.PostId == id);
        if (range > 0)
        {
            var start = DateTime.UtcNow.Date.AddDays(-range);
            q = q.Where(h => h.ClickedAtUtc >= start);
        }

        var raw = await q.Select(h => new { h.X, h.Y }).ToListAsync();
        var points = raw
            .GroupBy(h => (h.X, h.Y))
            .Select(g => new HeatmapPoint { X = g.Key.X, Y = g.Key.Y, Count = g.Count() })
            .OrderByDescending(p => p.Count)
            .Take(500)
            .ToList();

        ViewData["Title"] = _t["ana.heatmap"] + " — " + post.Title;
        return View(new HeatmapDetailViewModel
        {
            PostId = post.Id,
            Title = post.Title,
            Slug = post.Slug,
            RangeDays = range,
            TotalClicks = raw.Count,
            UniqueCells = points.Count,
            Points = points
        });
    }
}
