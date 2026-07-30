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
            trendingPosts.Add(MakeTop(p.Title, p.Slug, p.ViewCount, t.C));
        }

        var popularRows = await postQuery.AsNoTracking()
            .OrderByDescending(p => p.ViewCount)
            .Take(8)
            .Select(p => new { p.Id, p.Title, p.Slug, p.ViewCount })
            .ToListAsync();

        var popular = popularRows
            .Select(p => MakeTop(p.Title, p.Slug, p.ViewCount, rangeViewsByPost.GetValueOrDefault(p.Id)))
            .ToList();

        var searchLogs = await _db.SearchQueryLogs.AsNoTracking()
            .Where(s => s.SearchedAtUtc >= rangeStart)
            .ToListAsync();
        var searchKw = searchLogs
            .GroupBy(s => s.Query.Trim().ToLowerInvariant())
            .Select(g => new NamedCount { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(15)
            .ToList();

        var heatmapClicks = myPostIds.Count == 0
            ? 0
            : await _db.HeatmapClicks.AsNoTracking()
                .CountAsync(h => h.ClickedAtUtc >= rangeStart && myPostIds.Contains(h.PostId));

        ApiAnalyticsPanel? apiPanel = null;
        if (AuthorAccess.IsSuperAdmin(User))
        {
            try { apiPanel = await BuildApiPanelAsync(range); }
            catch { /* table may not exist yet on first boot */ }
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

    [HttpGet]
    public async Task<IActionResult> HeatmapsData()
    {
        var req = DataTablesRequest.From(Request);
        var userId = AuthorAccess.UserId(User)!;
        var seeAll = AuthorAccess.CanViewAllAnalytics(User);

        var query = _db.Posts.AsNoTracking().Where(p => !p.IsDeleted);
        if (!seeAll)
            query = query.Where(p => p.AuthorId == userId);

        var total = await query.CountAsync();

        if (!string.IsNullOrWhiteSpace(req.SearchValue))
        {
            var term = req.SearchValue;
            query = query.Where(p => p.Title.Contains(term) || p.Slug.Contains(term));
        }

        var filtered = await query.CountAsync();

        var postIds = await query.Select(p => p.Id).ToListAsync();
        var clickMap = postIds.Count == 0
            ? new Dictionary<int, int>()
            : await _db.HeatmapClicks.AsNoTracking()
                .Where(h => postIds.Contains(h.PostId))
                .GroupBy(h => h.PostId)
                .Select(g => new { PostId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.PostId, x => x.Count);

        List<Post> page;
        if (req.OrderColumn == 3)
        {
            var all = await query.ToListAsync();
            all = req.Asc
                ? all.OrderBy(p => clickMap.GetValueOrDefault(p.Id)).ToList()
                : all.OrderByDescending(p => clickMap.GetValueOrDefault(p.Id)).ToList();
            page = all.Skip(req.Start).Take(req.Length).ToList();
        }
        else
        {
            query = (req.OrderColumn, req.Asc) switch
            {
                (1, true) => query.OrderBy(p => p.Title),
                (1, false) => query.OrderByDescending(p => p.Title),
                (2, true) => query.OrderBy(p => p.ViewCount),
                (2, false) => query.OrderByDescending(p => p.ViewCount),
                (4, true) => query.OrderBy(p => p.CreatedAtUtc),
                (4, false) => query.OrderByDescending(p => p.CreatedAtUtc),
                _ => query.OrderByDescending(p => p.ViewCount)
            };
            page = await query.Skip(req.Start).Take(req.Length).ToList();
        }

        var openLabel = System.Net.WebUtility.HtmlEncode(_t["ana.heatmap_open"]);
        var rows = page.Select((p, i) =>
        {
            var clicks = clickMap.GetValueOrDefault(p.Id);
            var detailUrl = Url.Action("Heatmap", new { id = p.Id }) ?? "#";
            var titleHtml =
                $"<a href=\"{detailUrl}\" dir=\"auto\">{System.Net.WebUtility.HtmlEncode(p.Title)}</a>";
            var actionHtml =
                $"<a class=\"icon-btn\" href=\"{detailUrl}\">{openLabel}</a>";
            return new object[]
            {
                req.Start + i + 1,
                titleHtml,
                p.ViewCount,
                clicks,
                PersianDate.Date(p.CreatedAtUtc),
                actionHtml
            };
        }).ToList();

        return Json(DataTablesResponse.Ok(req.Draw, total, filtered, rows));
    }

    [HttpGet]
    public async Task<IActionResult> Heatmap(int id, int range = 30)
    {
        if (range is not (7 or 30 or 90 or 0)) range = 30;

        var userId = AuthorAccess.UserId(User)!;
        var seeAll = AuthorAccess.CanViewAllAnalytics(User);

        var post = await _db.Posts.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (post is null) return NotFound();
        if (!seeAll && post.AuthorId != userId) return Forbid();

        var clickQuery = _db.HeatmapClicks.AsNoTracking().Where(h => h.PostId == id);
        if (range > 0)
        {
            var rangeStart = DateTime.UtcNow.Date.AddDays(-(range - 1));
            clickQuery = clickQuery.Where(h => h.ClickedAtUtc >= rangeStart);
        }

        var clicks = await clickQuery.ToListAsync();
        var points = clicks
            .GroupBy(c => (c.X / 40) * 40 + "," + (c.Y / 40) * 40)
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

        ViewData["Title"] = _t["ana.heatmap"] + ": " + post.Title;
        return View(new HeatmapDetailViewModel
        {
            PostId = post.Id,
            Title = post.Title,
            Slug = post.Slug,
            RangeDays = range,
            TotalClicks = clicks.Count,
            UniqueCells = points.Count,
            Points = points
        });
    }
}
