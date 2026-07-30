using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

[Authorize(Roles = AppRoles.Reader + "," + AppRoles.Author + "," + AppRoles.SuperAdmin)]
public class SocialController : Controller
{
    private readonly ApplicationDbContext _db;

    public SocialController(ApplicationDbContext db) => _db = db;

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLike(int postId, string? returnUrl = null)
    {
        var userId = AuthorAccess.UserId(User)!;
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted);
        if (post is null) return NotFound();

        // Track for counter update
        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        post = await _db.Posts.FirstAsync(p => p.Id == postId);

        var existing = await _db.PostLikes
            .FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId);

        if (existing is null)
        {
            _db.PostLikes.Add(new PostLike { PostId = postId, UserId = userId, CreatedAtUtc = DateTime.UtcNow });
            post.LikeCount = Math.Max(0, post.LikeCount) + 1;
            ActivityWriter.Write(_db, userId, ActivityKind.PostLiked, postId: postId,
                title: post.Title, linkUrl: $"/post/{post.Slug}");
        }
        else
        {
            _db.PostLikes.Remove(existing);
            post.LikeCount = Math.Max(0, post.LikeCount - 1);
        }

        await _db.SaveChangesAsync();

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { liked = existing is null, count = post.LikeCount });

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Details", "Posts", new { slug = post.Slug });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> React(int postId, ReactionKind kind, string? returnUrl = null)
    {
        if (!Enum.IsDefined(kind)) return BadRequest();
        var userId = AuthorAccess.UserId(User)!;
        var post = await _db.Posts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted);
        if (post is null) return NotFound();

        var existing = await _db.PostReactions
            .FirstOrDefaultAsync(r => r.PostId == postId && r.UserId == userId);

        if (existing is null)
        {
            _db.PostReactions.Add(new PostReaction
            {
                PostId = postId,
                UserId = userId,
                Kind = kind,
                CreatedAtUtc = DateTime.UtcNow
            });
            ActivityWriter.Write(_db, userId, ActivityKind.PostReaction, postId: postId,
                title: post.Title, linkUrl: $"/post/{post.Slug}", meta: kind.ToString());
        }
        else if (existing.Kind == kind)
        {
            _db.PostReactions.Remove(existing);
        }
        else
        {
            existing.Kind = kind;
            existing.CreatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Details", "Posts", new { slug = post.Slug });
    }
}
