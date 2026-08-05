using BlogApp.Models;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public partial class AdminController
{
    /// <summary>
    /// REST snapshot for admin dashboard KPIs — used to correct SSE drift and on reconnect.
    /// Always scopes to non-deleted posts so totals match SSR and never jump high→low.
    /// </summary>
    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> LiveSnapshot(int range = 30)
    {
        if (range is not (7 or 30 or 90)) range = 30;
        var userId = AuthorAccess.UserId(User)!;
        var seeAll = AuthorAccess.CanViewAllAnalytics(User);
        var today = DateTime.UtcNow.Date;
        var rangeStart = today.AddDays(-(range - 1));

        // Authoritative scope: never include soft-deleted posts in any view KPI.
        var postQuery = _db.Posts.AsNoTracking().Where(p => !p.IsDeleted);
        if (!seeAll) postQuery = postQuery.Where(p => p.AuthorId == userId);
        var myPostIds = await postQuery.Select(p => p.Id).ToListAsync();

        // PostViews only for non-deleted posts (same set as postQuery).
        var viewsQuery = _db.PostViews.AsNoTracking()
            .Where(v => myPostIds.Contains(v.PostId));

        var viewsToday = myPostIds.Count == 0
            ? 0
            : await viewsQuery.CountAsync(v => v.ViewedAtUtc >= today);
        var viewsRange = myPostIds.Count == 0
            ? 0
            : await viewsQuery.CountAsync(v => v.ViewedAtUtc >= rangeStart);

        // Lifetime total must match Index SSR: sum of ViewCount on non-deleted posts
        // (not raw PostViews row count, which can diverge after resets / pre-tracking data).
        var viewsTotal = myPostIds.Count == 0
            ? 0
            : await postQuery.SumAsync(p => (int?)p.ViewCount) ?? 0;

        var commentQuery = _db.Comments.AsNoTracking()
            .Where(c => myPostIds.Contains(c.PostId));
        var pending = await commentQuery.CountAsync(c => c.Status == CommentStatus.Pending);
        var approved = await commentQuery.CountAsync(c => c.Status == CommentStatus.Approved);
        var rejected = await commentQuery.CountAsync(c => c.Status == CommentStatus.Rejected);

        var viewsByDay = myPostIds.Count == 0
            ? new List<(DateTime Day, int Count)>()
            : (await _db.PostViews.AsNoTracking()
                .Where(v => v.ViewedAtUtc >= rangeStart && myPostIds.Contains(v.PostId))
                .GroupBy(v => v.ViewedAtUtc.Date)
                .Select(g => new { Day = g.Key, Count = g.Count() })
                .ToListAsync())
                .Select(x => (x.Day, x.Count))
                .ToList();

        var series = new List<object>();
        for (var d = rangeStart; d <= today; d = d.AddDays(1))
        {
            var count = viewsByDay.FirstOrDefault(x => x.Day == d).Count;
            series.Add(new { label = d.ToString("MM-dd"), value = count });
        }

        var topRaw = myPostIds.Count == 0
            ? new List<(int Id, string Title, string Slug, int ViewCount)>()
            : (await postQuery
                .OrderByDescending(p => p.ViewCount)
                .Take(5)
                .Select(p => new { p.Id, p.Title, p.Slug, p.ViewCount })
                .ToListAsync())
                .Select(p => (p.Id, p.Title, p.Slug, p.ViewCount))
                .ToList();

        var rangeCounts = myPostIds.Count == 0
            ? new List<(int PostId, int Count)>()
            : (await _db.PostViews.AsNoTracking()
                .Where(v => v.ViewedAtUtc >= rangeStart && myPostIds.Contains(v.PostId))
                .GroupBy(v => v.PostId)
                .Select(g => new { PostId = g.Key, Count = g.Count() })
                .ToListAsync())
                .Select(x => (x.PostId, x.Count))
                .ToList();
        var rangeMap = rangeCounts.ToDictionary(x => x.PostId, x => x.Count);
        var top = topRaw.Select(p => new
        {
            slug = p.Slug,
            title = p.Title,
            views = p.ViewCount,
            rangeViews = rangeMap.GetValueOrDefault(p.Id, 0)
        }).ToList();

        return Json(new
        {
            ok = true,
            at = DateTime.UtcNow,
            range,
            viewsToday,
            viewsRange,
            viewsTotal,
            pending,
            approved,
            rejected,
            series,
            topPosts = top
        });
    }
}
