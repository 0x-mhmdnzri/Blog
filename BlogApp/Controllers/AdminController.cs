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

        // Exclude soft-deleted posts so initial KPIs match LiveSnapshot/SSE (no high→low flicker).
        var postQuery = _db.Posts.AsQueryable().Where(p => !p.IsDeleted);
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
            : Math.Round((currentRangeViews.Count - previousRangeViews.Count) * 100.0 / previousRangeViews.Count, 1);

        var recentPostDates = await postQuery
            .Where(p => p.CreatedAtUtc >= today.AddMonths(-5))
            .Select(p => p.CreatedAtUtc)
            .ToListAsync();

        var postsByMonth = new List<ChartPoint>();
        for (var i = 5; i >= 0; i--)
        {
            var month = today.AddMonths(-i);
            var label = month.ToString("yyyy-MM");
            postsByMonth.Add(new ChartPoint
            {
                Label = label,
                Value = recentPostDates.Count(d => d.Year == month.Year && d.Month == month.Month)
            });
        }

        var postsByCategory = await postQuery
            .GroupBy(p => p.Category != null ? p.Category.Name : _t["msg.uncategorized"])
            .Select(g => new ChartPoint { Label = g.Key, Value = g.Count() })
            .OrderByDescending(x => x.Value)
            .Take(8)
            .ToListAsync();

        var topPostsRaw = await postQuery
            .OrderByDescending(p => p.ViewCount)
            .Take(5)
            .Select(p => new { p.Id, p.Title, p.Slug, p.ViewCount })
            .ToListAsync();

        var topIds = topPostsRaw.Select(p => p.Id).ToList();
        var topRangeViews = currentRangeViews
            .Where(v => topIds.Contains(v.PostId))
            .GroupBy(v => v.PostId)
            .ToDictionary(g => g.Key, g => g.Count());

        var topPosts = topPostsRaw.Select(p => new AdminTopPostItem
        {
            Title = p.Title,
           Slug = p.Slug,
            Views = p.ViewCount,
            RangeViews = topRangeViews.GetValueOrDefault(p.Id, 0)
        }).ToList();

        var commentQuery = _db.Comments.AsNoTracking()
            .Where(c => myPostIds.Contains(c.PostId));

        var currentUser = await _userManager.GetUserAsync(User);

        var vm = new AdminDashboardViewModel
        {
            TotalPosts = await postQuery.CountAsync(),
            PublishedPosts = await postQuery.CountAsync(p => p.IsPublished),
            DraftPosts = await postQuery.CountAsync(p => !p.IsPublished),
            PendingComments = await commentQuery.CountAsync(c => c.Status == CommentStatus.Pending),
            ApprovedComments = await commentQuery.CountAsync(c => c.Status == CommentStatus.Approved),
            RejectedComments = await commentQuery.CountAsync(c => c.Status == CommentStatus.Rejected),
            TotalMedia = seeAll
                ? await _db.MediaAssets.CountAsync()
                : await _db.MediaAssets.CountAsync(m => m.PostId != null && myPostIds.Contains(m.PostId.Value)),
            TotalMediaBytes = seeAll
                ? await _db.MediaAssets.SumAsync(m => (long?)m.SizeBytes) ?? 0
                : await _db.MediaAssets.Where(m => m.PostId != null && myPostIds.Contains(m.PostId.Value)).SumAsync(m => (long?)m.SizeBytes) ?? 0,
            TotalViews = await postQuery.SumAsync(p => (int?)p.ViewCount) ?? 0,
            ViewsToday = currentRangeViews.Count(v => v.ViewedAtUtc.Date == today),
            ViewsThisRange = currentRangeViews.Count,
            ViewsPreviousRange = previousRangeViews.Count,
            ViewsTrendPercent = trendPercent,
            RangeDays = range,
            ViewsByDay = viewsByDay,
            PostsByMonth = postsByMonth,
            PostsByCategory = postsByCategory,
            TopPosts = topPosts,
            RecentComments = await commentQuery
                .Include(c => c.Post)
                .OrderByDescending(c => c.CreatedAtUtc)
                .Take(6)
                .Select(c => new AdminCommentListItem
                {
                    Id = c.Id,
                    AuthorName = c.AuthorName,
                    Body = c.Body,
                    CreatedAtUtc = c.CreatedAtUtc,
                    Status = c.Status,
                    PostId = c.PostId,
                    PostTitle = c.Post.Title,
                    PostSlug = c.Post.Slug
                })
                .ToListAsync(),
            DisplayName = currentUser?.DisplayName ?? User.Identity?.Name ?? "",
            IsSuperAdmin = AuthorAccess.IsSuperAdmin(User),
            ScopeLabel = seeAll ? _t["msg.scope_all"] : _t["msg.scope_mine"]
        };

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> ResetAnalytics()
    {
        await _db.PostViews.ExecuteDeleteAsync();
        await _db.Posts.ExecuteUpdateAsync(s => s.SetProperty(p => p.ViewCount, 0));
        TempData["AnalyticsReset"] = _t["msg.analytics_reset"];
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveComment([FromForm] int id, [FromForm] string? returnStatus)
    {
        if (id <= 0)
        {
            return BadRequest();
        }

        var comment = await _db.Comments.Include(c => c.Post).FirstOrDefaultAsync(c => c.Id == id);
        if (comment is null) return NotFound();

        if (!AuthorAccess.CanModeratePost(User, comment.Post))
            return Forbid();

        comment.Status = CommentStatus.Approved;
        await _db.SaveChangesAsync();
        _broadcaster.Publish(new { type = "comment", status = "approved", id = comment.Id });

        if (!string.IsNullOrEmpty(returnStatus))
            return RedirectToAction("Comments", new { status = returnStatus });
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectComment([FromForm] int id, [FromForm] string? returnStatus)
    {
        if (id <= 0) return BadRequest();

        var comment = await _db.Comments.Include(c => c.Post).FirstOrDefaultAsync(c => c.Id == id);
        if (comment is null) return NotFound();

        if (!AuthorAccess.CanModeratePost(User, comment.Post))
            return Forbid();

        comment.Status = CommentStatus.Rejected;
        await _db.SaveChangesAsync();
        _broadcaster.Publish(new { type = "comment", status = "rejected", id = comment.Id });

        if (!string.IsNullOrEmpty(returnStatus))
            return RedirectToAction("Comments", new { status = returnStatus });
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment([FromForm] int id, [FromForm] string? returnStatus)
    {
        if (id <= 0) return BadRequest();

        var comment = await _db.Comments.Include(c => c.Post).FirstOrDefaultAsync(c => c.Id == id);
        if (comment is null) return NotFound();

        if (!AuthorAccess.CanModeratePost(User, comment.Post))
            return Forbid();

        _db.Comments.Remove(comment);
        await _db.SaveChangesAsync();
        _broadcaster.Publish(new { type = "comment", status = "deleted", id = id });

        if (!string.IsNullOrEmpty(returnStatus))
            return RedirectToAction("Comments", new { status = returnStatus });
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task Stream(CancellationToken cancellationToken)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        var (id, reader) = _broadcaster.Subscribe();
        try
        {
            await Response.WriteAsync("data: {\"type\":\"hello\"}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                var json = await reader.ReadAsync(cancellationToken);
                await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // client disconnected
        }
        finally
        {
            _broadcaster.Unsubscribe(id);
        }
    }
}
