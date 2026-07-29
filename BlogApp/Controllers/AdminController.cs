using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

/// <summary>
/// The author-only admin panel: dashboard, comment moderation (approve/reject), and post
/// management. Rendered with its own RTL / Persian / Vazirmatn layout (_AdminLayout.cshtml),
/// separate from the public-facing blog theme.
/// </summary>
[Authorize]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly AnalyticsBroadcaster _broadcaster;

    public AdminController(ApplicationDbContext db, AnalyticsBroadcaster broadcaster)
    {
        _db = db;
        _broadcaster = broadcaster;
    }

    public async Task<IActionResult> Index(int range = 30)
    {
        if (range != 7 && range != 30 && range != 90) range = 30;

        var today = DateTime.UtcNow.Date;
        var rangeStart = today.AddDays(-(range - 1));
        var previousRangeStart = rangeStart.AddDays(-range);

        // One query covers both the current and the previous comparison window; the rest
        // of the split happens in memory, which is plenty fast at this data scale.
        var recentViews = await _db.PostViews
            .Where(v => v.ViewedAtUtc >= previousRangeStart)
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

        // Posts created per month, last 6 months.
        var sixMonthsAgo = new DateTime(today.Year, today.Month, 1).AddMonths(-5);
        var recentPostDates = await _db.Posts
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

        var postsByCategory = await _db.Posts
            .GroupBy(p => p.Category != null ? p.Category.Name : "بدون دسته")
            .Select(g => new NamedCount { Name = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToListAsync();

        // Top posts by all-time views, annotated with how many of those views fall in the
        // selected range — mirrors how most analytics dashboards pair a leaderboard with a
        // recency window instead of only ever showing the same all-time list.
        var topPostsRaw = await _db.Posts
            .OrderByDescending(p => p.ViewCount)
            .Take(5)
            .Select(p => new { p.Id, p.Title, p.Slug, p.ViewCount })
            .ToListAsync();

        var topPosts = topPostsRaw.Select(p => new TopPostItem
        {
            Title = p.Title,
            Slug = p.Slug,
            Views = p.ViewCount,
            RangeViews = currentRangeViews.Count(v => v.PostId == p.Id)
        }).ToList();

        var vm = new AdminDashboardViewModel
        {
            TotalPosts = await _db.Posts.CountAsync(),
            PublishedPosts = await _db.Posts.CountAsync(p => p.IsPublished),
            DraftPosts = await _db.Posts.CountAsync(p => !p.IsPublished),
            PendingComments = await _db.Comments.CountAsync(c => c.Status == CommentStatus.Pending),
            ApprovedComments = await _db.Comments.CountAsync(c => c.Status == CommentStatus.Approved),
            RejectedComments = await _db.Comments.CountAsync(c => c.Status == CommentStatus.Rejected),
            TotalMedia = await _db.MediaAssets.CountAsync(),
            TotalMediaBytes = await _db.MediaAssets.SumAsync(m => (long?)m.SizeBytes) ?? 0,
            TotalViews = await _db.Posts.SumAsync(p => (int?)p.ViewCount) ?? 0,
            ViewsToday = currentRangeViews.Count(v => v.ViewedAtUtc.Date == today),
            ViewsThisRange = currentRangeViews.Count,
            ViewsPreviousRange = previousRangeViews.Count,
            ViewsTrendPercent = trendPercent,
            RangeDays = range,
            ViewsByDay = viewsByDay,
            PostsByMonth = postsByMonth,
            PostsByCategory = postsByCategory,
            TopPosts = topPosts,
            RecentComments = await _db.Comments
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
                .ToListAsync()
        };

        return View(vm);
    }

    public async Task<IActionResult> Comments(string status = "pending")
    {
        var query = _db.Comments.Include(c => c.Post).AsQueryable();

        query = status switch
        {
            "approved" => query.Where(c => c.Status == CommentStatus.Approved),
            "rejected" => query.Where(c => c.Status == CommentStatus.Rejected),
            "all" => query,
            _ => query.Where(c => c.Status == CommentStatus.Pending)
        };

        var items = await query
            .OrderByDescending(c => c.CreatedAtUtc)
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
            .ToListAsync();

        ViewBag.CurrentStatus = status;
        ViewBag.PendingCount = await _db.Comments.CountAsync(c => c.Status == CommentStatus.Pending);
        ViewBag.ApprovedCount = await _db.Comments.CountAsync(c => c.Status == CommentStatus.Approved);
        ViewBag.RejectedCount = await _db.Comments.CountAsync(c => c.Status == CommentStatus.Rejected);
        ViewBag.AllCount = await _db.Comments.CountAsync();

        return View(items);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveComment(int id, string? returnStatus)
    {
        var comment = await _db.Comments.FindAsync(id);
        if (comment is not null)
        {
            comment.Status = CommentStatus.Approved;
            await _db.SaveChangesAsync();
            _broadcaster.Publish(new { type = "comment", status = "approved", commentId = id });
        }
        return RedirectToAction(nameof(Comments), new { status = returnStatus ?? "pending" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectComment(int id, string? returnStatus)
    {
        var comment = await _db.Comments.FindAsync(id);
        if (comment is not null)
        {
            comment.Status = CommentStatus.Rejected;
            await _db.SaveChangesAsync();
            _broadcaster.Publish(new { type = "comment", status = "rejected", commentId = id });
        }
        return RedirectToAction(nameof(Comments), new { status = returnStatus ?? "pending" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment(int id, string? returnStatus)
    {
        var comment = await _db.Comments.FindAsync(id);
        if (comment is not null)
        {
            _db.Comments.Remove(comment);
            await _db.SaveChangesAsync();
            _broadcaster.Publish(new { type = "comment", status = "deleted", commentId = id });
        }
        return RedirectToAction(nameof(Comments), new { status = returnStatus ?? "pending" });
    }

    /// <summary>
    /// Server-Sent Events stream of live dashboard activity (new views, new/moderated
    /// comments). The admin layout opens one connection per browser tab and keeps it open
    /// for the life of the page.
    /// </summary>
    [HttpGet]
    public async Task Stream(CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no"; // disable nginx buffering, if fronted by one

        var bufferingFeature = HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>();
        bufferingFeature?.DisableBuffering();

        var (id, reader) = _broadcaster.Subscribe();
        try
        {
            await Response.WriteAsync(": connected\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                using var heartbeat = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, heartbeat.Token);

                string? message = null;
                try
                {
                    message = await reader.ReadAsync(linked.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Heartbeat tick, not a real disconnect — fall through and send a comment
                    // line to keep the connection alive through proxies/load balancers.
                }

                if (cancellationToken.IsCancellationRequested) break;

                if (message is not null)
                    await Response.WriteAsync($"data: {message}\n\n", cancellationToken);
                else
                    await Response.WriteAsync(": ping\n\n", cancellationToken);

                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — expected when a browser tab closes or navigates away.
        }
        finally
        {
            _broadcaster.Unsubscribe(id);
        }
    }

    public async Task<IActionResult> Posts()
    {
        var items = await _db.Posts
            .Include(p => p.Category)
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new AdminPostListItem
            {
                Id = p.Id,
                Title = p.Title,
                Slug = p.Slug,
                CategoryName = p.Category != null ? p.Category.Name : null,
                IsPublished = p.IsPublished,
                CreatedAtUtc = p.CreatedAtUtc,
                ViewCount = p.ViewCount,
                CommentCount = p.Comments.Count
            })
            .ToListAsync();

        return View(items);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePublish(int id)
    {
        var post = await _db.Posts.FindAsync(id);
        if (post is not null)
        {
            post.IsPublished = !post.IsPublished;
            if (post.IsPublished && post.PublishedAtUtc is null)
                post.PublishedAtUtc = DateTime.UtcNow;
            post.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Posts));
    }

    // ---- Demo placeholders for the sidebar so the panel reads as a real product ----

    public IActionResult Media() => View("ComingSoon", new ComingSoonViewModel
    {
        Title = "رسانه‌ها",
        Description = "کتابخانه رسانه برای مرور، جست‌وجو و مدیریت همه تصاویر و ویدیوهای آپلودشده در یک صفحه — به‌زودی اضافه می‌شود."
    });

    public IActionResult CategoriesAdmin() => View("ComingSoon", new ComingSoonViewModel
    {
        Title = "دسته‌بندی‌ها و برچسب‌ها",
        Description = "افزودن، ویرایش و حذف دسته‌بندی‌ها و برچسب‌ها بدون نیاز به دیتابیس — به‌زودی اضافه می‌شود."
    });

    public IActionResult Settings() => View("ComingSoon", new ComingSoonViewModel
    {
        Title = "تنظیمات",
        Description = "تنظیمات نمایه نویسنده، شبکه‌های اجتماعی و پیکربندی سایت — به‌زودی اضافه می‌شود."
    });
}
