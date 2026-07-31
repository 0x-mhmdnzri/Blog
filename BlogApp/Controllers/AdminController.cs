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
            Slug = p.Slug,
            Views = p.ViewCount,
            RangeViews = currentRangeViews.Count(v => v.PostId == p.Id)
        }).ToList();

        var commentQuery = _db.Comments.AsQueryable();
        if (!seeAll)
            commentQuery = commentQuery.Where(c => myPostIds.Contains(c.PostId));

        var currentUser = await _userManager.GetUserAsync(User);

        ViewBag.CurrentUserId = userId;
        ViewBag.SeeAllAnalytics = seeAll;

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
            TempData["CommentModError"] = "شناسه دیدگاه نامعتبر است.";
            return CommentModResult(false, "شناسه دیدگاه نامعتبر است.", returnStatus);
        }

        var comment = await _db.Comments
            .IgnoreQueryFilters()
            .Include(c => c.Post)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (comment is null)
        {
            TempData["CommentModError"] = "دیدگاه پیدا نشد.";
            return CommentModResult(false, "دیدگاه پیدا نشد.", returnStatus);
        }

        if (!AuthorAccess.CanModerateComment(User, comment.Post))
        {
            TempData["CommentModError"] = "اجازه تایید این دیدگاه را ندارید.";
            return CommentModResult(false, "اجازه تایید این دیدگاه را ندارید.", returnStatus);
        }

        var wasApproved = comment.Status == CommentStatus.Approved;
        comment.Status = CommentStatus.Approved;
        await _db.SaveChangesAsync();

        _broadcaster.Publish(new
        {
            type = "comment",
            status = "approved",
            commentId = comment.Id,
            postId = comment.PostId,
            authorName = comment.AuthorName
        });

        if (!wasApproved && !string.IsNullOrEmpty(comment.UserId))
        {
            try
            {
                var notify = HttpContext.RequestServices.GetService<INotificationService>();
                if (notify is not null)
                {
                    var postTitle = comment.Post?.Title ?? "نوشته";
                    var slug = comment.Post?.Slug;
                    var link = string.IsNullOrEmpty(slug)
                        ? "/Notifications"
                        : $"/post/{slug}#comment-{comment.Id}";
                    await notify.NotifyAsync(
                        comment.UserId,
                        NotificationKind.CommentApproved,
                        "کامنت شما با تایید مدیریت منتشر شد",
                        $"دیدگاه شما روی «{postTitle}» تایید و منتشر شد.",
                        link);
                }
            }
            catch { }
        }

        TempData["CommentModOk"] = "دیدگاه تایید و منتشر شد.";
        return CommentModResult(true, "دیدگاه تایید و منتشر شد.", returnStatus ?? "pending");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectComment([FromForm] int id, [FromForm] string? returnStatus)
    {
        if (id <= 0)
            return CommentModResult(false, "شناسه دیدگاه نامعتبر است.", returnStatus);

        var comment = await _db.Comments.IgnoreQueryFilters()
            .Include(c => c.Post).FirstOrDefaultAsync(c => c.Id == id);
        if (comment is null)
            return CommentModResult(false, "دیدگاه پیدا نشد.", returnStatus);
        if (!AuthorAccess.CanModerateComment(User, comment.Post))
            return CommentModResult(false, "اجازه رد این دیدگاه را ندارید.", returnStatus);

        comment.Status = CommentStatus.Rejected;
        await _db.SaveChangesAsync();
        _broadcaster.Publish(new { type = "comment", status = "rejected", commentId = id });
        TempData["CommentModOk"] = "دیدگاه رد شد.";
        return CommentModResult(true, "دیدگاه رد شد.", returnStatus ?? "pending");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment([FromForm] int id, [FromForm] string? returnStatus)
    {
        if (id <= 0)
            return CommentModResult(false, "شناسه دیدگاه نامعتبر است.", returnStatus);

        var comment = await _db.Comments.IgnoreQueryFilters()
            .Include(c => c.Post).FirstOrDefaultAsync(c => c.Id == id);
        if (comment is null)
            return CommentModResult(false, "دیدگاه پیدا نشد.", returnStatus);
        if (!AuthorAccess.CanModerateComment(User, comment.Post))
            return CommentModResult(false, "اجازه حذف این دیدگاه را ندارید.", returnStatus);

        _db.Comments.Remove(comment);
        await _db.SaveChangesAsync();
        _broadcaster.Publish(new { type = "comment", status = "deleted", commentId = id });
        TempData["CommentModOk"] = "دیدگاه حذف شد.";
        return CommentModResult(true, "دیدگاه حذف شد.", returnStatus ?? "pending");
    }

    private IActionResult CommentModResult(bool ok, string message, string? returnStatus)
    {
        var wantsJson =
            string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
            || (Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase)
                && !Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase));

        if (wantsJson)
            return Json(new { ok, message, redirect = Url.Action(nameof(Comments), new { status = returnStatus ?? "pending" }) });

        if (!ok)
            TempData["CommentModError"] = message;
        else
            TempData["CommentModOk"] = message;

        return RedirectToAction(nameof(Comments), new { status = returnStatus ?? "pending" });
    }

    [HttpGet]
    public async Task Stream(CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

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
                try { message = await reader.ReadAsync(linked.Token); }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }

                if (cancellationToken.IsCancellationRequested) break;

                if (message is not null)
                    await Response.WriteAsync($"data: {message}\n\n", cancellationToken);
                else
                    await Response.WriteAsync(": ping\n\n", cancellationToken);

                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
        finally { _broadcaster.Unsubscribe(id); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePublish(int id)
    {
        var post = await _db.Posts.FindAsync(id);
        if (post is not null && AuthorAccess.OwnsPost(User, post))
        {
            post.IsPublished = !post.IsPublished;
            if (post.IsPublished && post.PublishedAtUtc is null)
                post.PublishedAtUtc = DateTime.UtcNow;
            post.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Posts));
    }

    public IActionResult CategoriesAdmin() => View("ComingSoon", new ComingSoonViewModel
    {
        Title = _t["admin.nav.taxonomy"],
        Description = "Manage content structure without hand-editing the DB.",
        DemoFeatures =
        [
            "Add / edit / delete categories",
            "Nested categories",
            "Tag merge",
            "Post counts per category"
        ]
    });

    [HttpGet]
    public IActionResult Settings()
    {
        if (AuthorAccess.IsSuperAdmin(User))
            return RedirectToAction("Index", "AdminSettings");

        return View("ComingSoon", new ComingSoonViewModel
        {
            Title = _t["admin.nav.settings"],
            Description = "Site configuration is SuperAdmin-only.",
            DemoFeatures =
            [
                "Site name & description",
                "Maintenance mode",
                "Announcement banner",
                "Feature flags"
            ]
        });
    }

    [HttpGet]
    public IActionResult Newsletter()
    {
        if (AuthorAccess.IsSuperAdmin(User))
            return RedirectToAction("Index", "AdminNewsletter");

        return Forbid();
    }
}
