using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using BlogApp.Services.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

[Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
public partial class AdminController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly AnalyticsBroadcaster _broadcaster;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUiTranslator _t;

    public AdminController(
        ApplicationDbContext db,
        AnalyticsBroadcaster broadcaster,
        UserManager<ApplicationUser> userManager,
        IUiTranslator t)
    {
        _db = db;
        _broadcaster = broadcaster;
        _userManager = userManager;
        _t = t;
    }

    public async Task<IActionResult> Index(int range = 30)
    {
        if (range != 7 && range != 30 && range != 90) range = 30;

        var userId = AuthorAccess.UserId(User)!;
        var seeAll = AuthorAccess.CanViewAllAnalytics(User);

        var today = DateTime.UtcNow.Date;
        var rangeStart = today.AddDays(-(range - 1));
        var previousRangeStart = rangeStart.AddDays(-range);

        var postQuery = _db.Posts.AsQueryable();
        if (!seeAll)
            postQuery = postQuery.Where(p => p.AuthorId == userId);

        var myPostIds = await postQuery.Select(p => p.Id).ToListAsync();

        var recentViews = await _db.PostViews
            .Where(v => v.ViewedAtUtc >= previousRangeStart && myPostIds.Contains(v.PostId))
            .Select(v => new { v.PostId, v.ViewedAtUtc })
            .ToListAsync();

        var currentRangeViews = recentViews.Where(v => v.ViewedAtUtc >= rangeStart).ToList();
        var previousRangeViews = recentViews.Where(v => v.ViewedAtUtc < rangeStart).ToList();

        var viewsByDay = new List<ChartPoint>();
        for (var day = rangeStart; day <= today; day = day.AddDays(1))
        {
            viewsByDay.Add(new ChartPoint
            {
                Label = day.ToString("yyyy-MM-dd"),
                Value = currentRangeViews.Count(v => v.ViewedAtUtc.Date == day)
            });
        }

        double trendPercent = previousRangeViews.Count == 0
            ? (currentRangeViews.Count > 0 ? 100 : 0)
            : Math.Round((currentRangeViews.Count - previousRangeViews.Count) / (double)previousRangeViews.Count * 100, 1);

        var sixMonthsAgo = new DateTime(today.Year, today.Month, 1).AddMonths(-5);
        var recentPostDates = await postQuery
            .Where(p => p.CreatedAtUtc >= sixMonthsAgo)
            .Select(p => p.CreatedAtUtc)
            .ToListAsync();

        var postsByMonth = new List<ChartPoint>();
        for (var month = sixMonthsAgo; month <= today; month = month.AddMonths(1))
        {
            postsByMonth.Add(new ChartPoint
            {
                Label = month.ToString("yyyy-MM"),
                Value = recentPostDates.Count(d => d.Year == month.Year && d.Month == month.Month)
            });
        }

        var uncategorized = _t["msg.uncategorized"];
        var postsByCategory = await postQuery
            .GroupBy(p => p.Category != null ? p.Category.Name : uncategorized)
            .Select(g => new NamedCount { Name = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToListAsync();

        var topPostsRaw = await postQuery
            .OrderByDescending(p => p.ViewCount)
            .Take(5)
            .Select(p => new { p.Id, p.Title, p.Slug, p.ViewCount })
            .ToListAsync();

        var topPosts = topPostsRaw.Select(p => new TopPostItem
        {
            Title = p.Title,
            Spreadsheet = p.Slug,
            Views = p.ViewCount,
            RangeViews = currentRangeViews.Count(v => v.PostId == p.Id)
        }).ToList();

        return View();
    }
}
