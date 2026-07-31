using BlogApp.Models;
using BlogApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public partial class AdminController
{
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PinComment(int id, string? returnStatus)
    {
        var comment = await _db.Comments.AsTracking().Include(c => c.Post).FirstOrDefaultAsync(c => c.Id == id);
        if (comment is not null && AuthorAccess.CanModerateComment(User, comment.Post))
        {
            comment.IsPinned = true;
            comment.PinnedAtUtc = DateTime.UtcNow;
            if (comment.Status == CommentStatus.Pending || comment.Status == CommentStatus.Spam)
                comment.Status = CommentStatus.Approved;
            await _db.SaveChangesAsync();
            _broadcaster.Publish(new { type = "comment", status = "pinned", commentId = id });
        }
        return RedirectToAction(nameof(Comments), new { status = returnStatus ?? "approved" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UnpinComment(int id, string? returnStatus)
    {
        var comment = await _db.Comments.AsTracking().Include(c => c.Post).FirstOrDefaultAsync(c => c.Id == id);
        if (comment is not null && AuthorAccess.CanModerateComment(User, comment.Post))
        {
            comment.IsPinned = false;
            comment.PinnedAtUtc = null;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Comments), new { status = returnStatus ?? "approved" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkSpamComment(int id, string? returnStatus)
    {
        var comment = await _db.Comments.AsTracking().Include(c => c.Post).FirstOrDefaultAsync(c => c.Id == id);
        if (comment is not null && AuthorAccess.CanModerateComment(User, comment.Post))
        {
            comment.Status = CommentStatus.Spam;
            comment.IsPinned = false;
            comment.PinnedAtUtc = null;
            if (comment.SpamScore < 60) comment.SpamScore = 60;
            comment.SpamReasons = string.IsNullOrEmpty(comment.SpamReasons)
                ? "manual"
                : comment.SpamReasons + ",manual";
            await _db.SaveChangesAsync();
            _broadcaster.Publish(new { type = "comment", status = "spam", commentId = id });
        }
        return RedirectToAction(nameof(Comments), new { status = returnStatus ?? "spam" });
    }
}
