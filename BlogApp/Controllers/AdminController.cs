using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
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

    public AdminController(
        ApplicationDbContext db,
        AnalyticsBroadcaster broadcaster,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _broadcaster = broadcaster;
        _userManager = userManager;
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

        var postsByCategory = await postQuery
            .GroupBy(p => p.Category != null ? p.Category.Name : "بدون دسته")
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
            ScopeLabel = seeAll ? "همه نویسندگان" : "فقط نوشته‌های من"
        };

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> ResetAnalytics()
    {
        await _db.PostViews.ExecuteDeleteAsync();
        await _db.Posts.ExecuteUpdateAsync(s => s.SetProperty(p => p.ViewCount, 0));
        TempData["AnalyticsReset"] = "آمار بازدید پاک شد. از این به بعد فقط بازدیدهای واقعی ثبت می‌شوند.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveComment(int id, string? returnStatus)
    {
        var comment = await _db.Comments.Include(c => c.Post).FirstOrDefaultAsync(c => c.Id == id);
        if (comment is not null && AuthorAccess.OwnsPost(User, comment.Post))
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
        var comment = await _db.Comments.Include(c => c.Post).FirstOrDefaultAsync(c => c.Id == id);
        if (comment is not null && AuthorAccess.OwnsPost(User, comment.Post))
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
        var comment = await _db.Comments.Include(c => c.Post).FirstOrDefaultAsync(c => c.Id == id);
        if (comment is not null && AuthorAccess.OwnsPost(User, comment.Post))
        {
            _db.Comments.Remove(comment);
            await _db.SaveChangesAsync();
            _broadcaster.Publish(new { type = "comment", status = "deleted", commentId = id });
        }
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

    public IActionResult Media() => View("ComingSoon", new ComingSoonViewModel
    {
        Title = "رسانه‌ها",
        Description = "کتابخانه رسانه برای مرور و مدیریت تصاویر، ویدیو و فایل‌های آپلودشده.",
        DemoFeatures =
        [
            "گالری شبکه‌ای با پیش‌نمایش",
            "جست‌وجو بر اساس نام و نوع فایل",
            "فشرده‌سازی تصویر و تبدیل WebP",
            "حذف گروهی و جایگزینی در نوشته‌ها"
        ]
    });

    public IActionResult CategoriesAdmin() => View("ComingSoon", new ComingSoonViewModel
    {
        Title = "دسته‌بندی‌ها و برچسب‌ها",
        Description = "مدیریت ساختار محتوا بدون دست‌کاری دیتابیس به‌صورت دستی.",
        DemoFeatures =
        [
            "افزودن / ویرایش / حذف دسته",
            "دسته‌های تو در تو",
            "مدیریت برچسب‌ها و ادغام تکراری‌ها",
            "شمارش نوشته‌های هر دسته"
        ]
    });

    [HttpGet]
    public IActionResult Settings()
    {
        if (AuthorAccess.IsSuperAdmin(User))
            return RedirectToAction("Index", "AdminSettings");

        return View("ComingSoon", new ComingSoonViewModel
        {
            Title = "تنظیمات سایت",
            Description = "پیکربندی عمومی فقط برای SuperAdmin در دسترس است.",
            DemoFeatures =
            [
                "نام و توضیح سایت",
                "حالت نگهداری",
                "بنر اعلان سراسری",
                "پرچم‌های ویژگی"
            ]
        });
    }

    public IActionResult SeoTools() => View("ComingSoon", new ComingSoonViewModel
    {
        Title = "ابزارهای سئو",
        Description = "نقشه سایت، ریدایرکت و تشخیص لینک شکسته از پنل مدیریت.",
        DemoFeatures =
        [
            "مدیریت ریدایرکت ۳۰۱ / ۳۰۲",
            "اسکن لینک‌های شکسته در نوشته‌ها",
            "پیش‌نمایش متا و Open Graph",
            "خروجی sitemap.xml زنده"
        ]
    });

    public IActionResult Newsletter() => View("ComingSoon", new ComingSoonViewModel
    {
        Title = "خبرنامه",
        Description = "عضویت خوانندگان و ارسال خلاصه هفتگی.",
        DemoFeatures =
        [
            "فرم عضویت دو مرحله‌ای",
            "بخش‌بندی مخاطبان",
            "زمان‌بندی ارسال",
            "آمار بازشدن ایمیل"
        ]
    });
}
