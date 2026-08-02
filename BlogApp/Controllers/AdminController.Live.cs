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

        var postQuery = _db.Posts.AsQueryable().Where(p => !p.IsDeleted);
        if (!seeAll) postQuery = postQuery.Where(p => p.AuthorId == userId);
        var myPostIds = await postQuery.Select(p => p.Id).ToListAsync();

        var viewsQuery = _db.PostViews.AsNoTracking().Where(v => myPostIds.Contains(v.PostId));
        var viewsToday = await viewsQuery.CountAsync(v => v.ViewedAtUtc >= today);
        var viewsRange = await viewsQuery.CountAsync(v => v.ViewedAtUtc >= rangeStart);
        var viewsTotal = await viewsQuery.CountAsync();
        var viewCountSum = await postQuery.SumAsync(p => (int?)p.ViewCount) ?? 0;
        if (viewsTotal == 0 && viewCountSum > 0) viewsTotal = viewCountSum;

        var commentQuery = _db.Comments.AsNoTracking()
            .Where(c => myPostIds.Contains(c.PostId));
        var pending = await commentQuery.CountAsync(c => c.Status == CommentStatus.Pending);
        var approved = await commentQuery.CountAsync(c => c.Status == CommentStatus.Approved);
        var rejected = await commentQuery.CountAsync(c => c.Status == CommentStatus.Rejected);

        var viewsByDay = await _db.PostViews.AsNoTracking()
            .Where(v => v.ViewedAtUtc >= rangeStart && myPostIds.Contains(v.PostId))
            .GroupBy(v => v.ViewedAtUtc.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync();
        var dayMap = viewsByDay.ToDictionary(x => x.Day, x => x.Count);
        var series = new List<object>();
        for (var d = rangeStart; d <= today; d = d.AddDays(1))
            series.Add(new { label = d.ToString("yyyy-MM-dd"), value = dayMap.GetValueOrDefault(d, 0) });

        var topRaw = await postQuery.AsNoTracking()
            .OrderByDescending(p => p.ViewCount)
            .Take(5)
            .Select(p => new { p.Id, p.Slug, p.Title, p.ViewCount })
            .ToListAsync();
        var topIds = topRaw.Select(p => p.Id).ToList();
        var rangeCounts = await _db.PostViews.AsNoTracking()
            .Where(v => topIds.Contains(v.PostId) && v.ViewedAtUtc >= rangeStart)
            .GroupBy(v => v.PostId)
            .Select(g => new { PostId = g.Key, Count = g.Count() })
            .ToListAsync();
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
