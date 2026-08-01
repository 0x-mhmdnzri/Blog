using AVICRM.Data;
using AVICRM.Models;
using AVICRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AVICRM.Controllers;

/// <summary>
/// Bookmarks require any authenticated account (Reader, Author, or SuperAdmin).
/// Guests are challenged → Login with returnUrl back to the post.
/// </summary>
[Authorize(Roles = AppRoles.Reader + "," + AppRoles.Author + "," + AppRoles.SuperAdmin)]
public class BookmarksController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<BookmarksController> _logger;

    public BookmarksController(ApplicationDbContext db, ILogger<BookmarksController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = AuthorAccess.UserId(User)!;
        var items = await _db.PostBookmarks
            .AsNoTracking()
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAtUtc)
            .Select(b => new BookmarkListItem
            {
                PostId = b.PostId,
                Title = b.Post.Title,
                Slug = b.Post.Slug,
                Summary = b.Post.Summary,
                SavedAtUtc = b.CreatedAtUtc,
                IsPublished = b.Post.IsPublished && !b.Post.IsDeleted
            })
            .ToListAsync();

        ViewData["Title"] = "نشان‌ها";
        return View(items);
    }

    /// <summary>Toggle bookmark; returns to the post page.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int postId, string? returnUrl = null)
    {
        var userId = AuthorAccess.UserId(User)!;

        var post = await _db.Posts.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted && p.IsPublished);
        if (post is null) return NotFound();

        var existing = await _db.PostBookmarks
            .FirstOrDefaultAsync(b => b.UserId == userId && b.PostId == postId);

        if (existing is null)
        {
            _db.PostBookmarks.Add(new PostBookmark
            {
                UserId = userId,
                PostId = postId,
                CreatedAtUtc = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
            _logger.LogInformation("Bookmark added UserId={UserId} PostId={PostId}", userId, postId);
            TempData["BookmarkMsg"] = "نشان‌گذاری شد.";
        }
        else
        {
            _db.PostBookmarks.Remove(existing);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Bookmark removed UserId={UserId} PostId={PostId}", userId, postId);
            TempData["BookmarkMsg"] = "نشان برداشته شد.";
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Details", "Posts", new { slug = post.Slug });
    }
}

public class BookmarkListItem
{
    public int PostId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public DateTime SavedAtUtc { get; set; }
    public bool IsPublished { get; set; }
}
