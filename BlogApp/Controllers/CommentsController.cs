using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BlogApp.Controllers;

/// <summary>Comment likes, author edit window, staff pin. Login required.</summary>
public class CommentsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICommentSpamService _spam;
    private readonly CommentSpamOptions _opt;
    private readonly ILogger<CommentsController> _logger;

    public CommentsController(
        ApplicationDbContext db,
        ICommentSpamService spam,
        IOptions<CommentSpamOptions> opt,
        ILogger<CommentsController> logger)
    {
        _db = db;
        _spam = spam;
        _opt = opt.Value;
        _logger = logger;
    }

    [Authorize(Roles = AppRoles.Reader + "," + AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLike(int commentId, string? returnUrl = null)
    {
        var userId = AuthorAccess.UserId(User)!;
        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;

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

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string body, string? returnUrl = null)
    {
        body = (body ?? string.Empty).Trim();
        body = new string(body.Where(c => c is '\n' or '\r' or '\t' || !char.IsControl(c)).ToArray());

        if (body.Length is < 2 or > 2000)
        {
            TempData["CommentSubmitted"] = "متن دیدگاه معتبر نیست.";
            return LocalRedirectOrPost(returnUrl, null);
        }

        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;

        var comment = await _db.Comments.Include(c => c.Post).FirstOrDefaultAsync(c => c.Id == id);
        if (comment is null) return NotFound();

        var userId = AuthorAccess.UserId(User);
        var isOwner = !string.IsNullOrEmpty(comment.UserId) && comment.UserId == userId;
        var isStaff = AuthorAccess.OwnsPost(User, comment.Post);

        if (!isOwner && !isStaff)
            return Forbid();

        if (isOwner && !isStaff)
        {
            var window = TimeSpan.FromMinutes(Math.Max(1, _opt.EditWindowMinutes));
            if (DateTime.UtcNow - comment.CreatedAtUtc > window)
            {
                TempData["CommentSubmitted"] = $"مهلت ویرایش ({_opt.EditWindowMinutes} دقیقه) به پایان رسیده است.";
                return LocalRedirectOrPost(returnUrl, comment.Post);
            }
        }

        var spam = _spam.Evaluate(comment.AuthorName, body, comment.AuthorEmail, comment.IsGuest);
        comment.Body = body;
        comment.EditedAtUtc = DateTime.UtcNow;
        comment.EditCount++;
        comment.SpamScore = spam.Score;
        comment.SpamReasons = spam.Reasons.Count > 0 ? string.Join(",", spam.Reasons) : null;
        if (spam.IsSpam && comment.Status == CommentStatus.Approved)
            comment.Status = CommentStatus.Pending;

        await _db.SaveChangesAsync();
        TempData["CommentSubmitted"] = "دیدگاه به‌روزرسانی شد.";
        return LocalRedirectOrPost(returnUrl, comment.Post);
    }

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePin(int id, string? returnUrl = null)
    {
        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;

        var comment = await _db.Comments.Include(c => c.Post).FirstOrDefaultAsync(c => c.Id == id);
        if (comment is null) return NotFound();
        if (!AuthorAccess.OwnsPost(User, comment.Post))
            return Forbid();

        comment.IsPinned = !comment.IsPinned;
        comment.PinnedAtUtc = comment.IsPinned ? DateTime.UtcNow : null;
        await _db.SaveChangesAsync();

        TempData["CommentSubmitted"] = comment.IsPinned ? "دیدگاه سنجاق شد." : "سنجاق برداشته شد.";
        return LocalRedirectOrPost(returnUrl, comment.Post);
    }

    private IActionResult LocalRedirectOrPost(string? returnUrl, Post? post)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        if (post is not null)
            return Redirect($"/{post.LanguageCode}/post/{post.Slug}");
        return RedirectToAction("Index", "Home");
    }
}
