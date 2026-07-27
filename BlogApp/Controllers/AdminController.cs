using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
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

    public AdminController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var vm = new AdminDashboardViewModel
        {
            TotalPosts = await _db.Posts.CountAsync(),
            PublishedPosts = await _db.Posts.CountAsync(p => p.IsPublished),
            DraftPosts = await _db.Posts.CountAsync(p => !p.IsPublished),
            PendingComments = await _db.Comments.CountAsync(c => c.Status == CommentStatus.Pending),
            ApprovedComments = await _db.Comments.CountAsync(c => c.Status == CommentStatus.Approved),
            RejectedComments = await _db.Comments.CountAsync(c => c.Status == CommentStatus.Rejected),
            TotalMedia = await _db.MediaAssets.CountAsync(),
            TotalViews = await _db.Posts.SumAsync(p => (int?)p.ViewCount) ?? 0,
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
        }
        return RedirectToAction(nameof(Comments), new { status = returnStatus ?? "pending" });
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
