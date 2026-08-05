using BlogApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public partial class AdminAnalyticsController
{
    /// <summary>REST snapshot for /AdminAnalytics — corrects SSE drift. Scoped to non-deleted posts only.</summary>
    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> LiveSnapshot(int range = 30)
    {
        if (range is not (7 or 30 or 90)) range = 30;
        var userId = AuthorAccess.UserId(User)!;
        var seeAll = AuthorAccess.CanViewAllAnalytics(User);
        var today = DateTime.UtcNow.Date;
        var rangeStart = today.AddDays(-(range - 1));

        // Never include soft-deleted posts in analytics KPIs.
        var postQuery = _db.Posts.AsNoTracking().Where(p => !p.IsDeleted);
        if (!seeAll) postQuery = postQuery.Where(p => p.AuthorId == userId);
        var myPostIds = await postQuery.Select(p => p.Id).ToListAsync();

        var views = myPostIds.Count == 0
            ? new List<(DateTime ViewedAtUtc, string VisitorHash, int PostId)>()
            : (await _db.PostViews.AsNoTracking()
                .Where(v => v.ViewedAtUtc >= rangeStart && myPostIds.Contains(v.PostId))
                .Select(v => new { v.ViewedAtUtc, v.VisitorHash, v.PostId })
                .ToListAsync())
                .Select(v => (ViewedAtUtc: v.ViewedAtUtc, VisitorHash: v.VisitorHash ?? "", PostId: v.PostId))
                .ToList();

        var unique = views.Select(v => v.VisitorHash).Where(h => h.Length > 0).Distinct().Count();
        var viewsByDay = new List<object>();
        for (var d = rangeStart; d <= today; d = d.AddDays(1))
            viewsByDay.Add(new { label = d.ToString("MM-dd"), value = views.Count(v => v.ViewedAtUtc.Date == d) });

        var sessions = await _db.AnalyticsSessions.AsNoTracking()
            .Where(s => s.StartedAtUtc >= rangeStart)
            .Select(s => s.PageViewCount)
            .ToListAsync();
        var bounced = sessions.Count(c => c <= 1);
        var bounceRate = sessions.Count == 0 ? 0 : Math.Round(bounced * 100.0 / sessions.Count, 1);

        var searchCount = await _db.SearchQueryLogs.AsNoTracking()
            .CountAsync(s => s.SearchedAtUtc >= rangeStart);
        var heatmapCount = myPostIds.Count == 0
            ? 0
            : await _db.HeatmapClicks.AsNoTracking()
                .CountAsync(h => h.ClickedAtUtc >= rangeStart && myPostIds.Contains(h.PostId));

        return Json(new
        {
            ok = true,
            at = DateTime.UtcNow,
            range,
            totalViews = views.Count,
            uniqueVisitors = unique,
            bounceRatePercent = bounceRate,
            sessionCount = sessions.Count,
            searchQueryCount = searchCount,
            heatmapClickCount = heatmapCount,
            viewsByDay
        });
    }
}
