using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

/// <summary>Comment likes — only authenticated Reader / Author / SuperAdmin.</summary>
[Authorize(Roles = AppRoles.Reader + "," + AppRoles.Author + "," + AppRoles.SuperAdmin)]
public class CommentsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<CommentsController> _logger;

    public CommentsController(ApplicationDbContext db, ILogger<CommentsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLike(int commentId, string? returnUrl = null)
    {
        var userId = AuthorAccess.UserId(User)!;

        var comment = await _db.Comments
            .Include(c => c.Post)
            .FirstOrDefaultAsync(c => c.Id == commentId && c.Status == CommentStatus.Approved);

        if (comment is null) return NotFound();

        var existing = await _db.CommentLikes
            .FirstOrDefaultAsync(l => l.CommentId == commentId && l.UserId == userId);

        if (existing is null)
        {
            _db.CommentLikes.Add(new CommentLike
            {
                CommentId = commentId,
                UserId = userId,
                CreatedAtUtc = DateTime.UtcNow
            });
            comment.LikeCount++;
            _logger.LogInformation("Comment liked CommentId={Id} UserId={UserId}", commentId, userId);
        }
        else
        {
            _db.CommentLikes.Remove(existing);
            comment.LikeCount = Math.Max(0, comment.LikeCount - 1);
            _logger.LogInformation("Comment unliked CommentId={Id} UserId={UserId}", commentId, userId);
        }

        await _db.SaveChangesAsync();

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Details", "Posts", new { slug = comment.Post.Slug });
    }
}
