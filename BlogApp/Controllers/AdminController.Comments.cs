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
        if (comment is null || !AuthorAccess.CanModerateComment(User, comment.Post))
            return RedirectToAction(nameof(Comments), new { status = returnStatus ?? "approved" });

        comment.IsPinned = true;
        comment.PinnedAtUtc = DateTime.UtcNow;
        if (comment.Status is CommentStatus.Pending or CommentStatus.Spam)
            comment.Status = CommentStatus.Approved;

        await _db.Comments.Where(c => c.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(c => c.IsPinned, true)
            .SetProperty(c => c.PinnedAtUtc, DateTime.UtcNow)
            .SetProperty(c => c.Status, comment.Status));

        _broadcaster.Publish(new { type = "comment", status = "pinned", commentId = id });
        return RedirectToAction(nameof(Comments), new { status = returnStatus ?? "approved" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UnpinComment(int id, string? returnStatus)
    {
        var comment = await _db.Comments.Include(c => c.Post).FirstOrDefaultAsync(c => c.Id == id);
        if (comment is null || !AuthorAccess.CanModerateComment(User, comment.Post))
            return RedirectToAction(nameof(Comments), new { status = returnStatus ?? "approved" });

        await _db.Comments.Where(c => c.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(c => c.IsPinned, false)
            .SetProperty(c => c.PinnedAtUtc, (DateTime?)null));

        return RedirectToAction(nameof(Comments), new { status = returnStatus ?? "approved" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkSpamComment(int id, string? returnStatus)
    {
        var comment = await _db.Comments.Include(c => c.Post).FirstOrDefaultAsync(c => c.Id == id);
        if (comment is null || !AuthorAccess.CanModerateComment(User, comment.Post))
            return RedirectToAction(nameof(Comments), new { status = returnStatus ?? "spam" });

        var score = comment.SpamScore < 60 ? 60 : comment.SpamScore;
        var reasons = string.IsNullOrEmpty(comment.SpamReasons) ? "manual" : comment.SpamReasons + ",manual";

        await _db.Comments.Where(c => c.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(c => c.Status, CommentStatus.Spam)
            .SetProperty(c => c.IsPinned, false)
            .SetProperty(c => c.PinnedAtUtc, (DateTime?)null)
            .SetProperty(c => c.SpamScore, score)
            .SetProperty(c => c.SpamReasons, reasons));

        _broadcaster.Publish(new { type = "comment", status = "spam", commentId = id });
        return RedirectToAction(nameof(Comments), new { status = returnStatus ?? "spam" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveComment([FromForm] int id, [FromForm] string? returnStatus, [FromForm] string? returnTo)
    {
        if (id <= 0)
            return BadRequest();

        // Global QueryTrackingBehavior is NoTracking — must AsTracking or ExecuteUpdate.
        var comment = await _db.Comments.AsTracking()
            .Include(c => c.Post)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (comment is null) return NotFound();

        if (!AuthorAccess.CanModerateComment(User, comment.Post))
            return Forbid();

        comment.Status = CommentStatus.Approved;
        await _db.SaveChangesAsync();
        _broadcaster.Publish(new { type = "comment", status = "approved", id = comment.Id });

        if (string.Equals(returnTo, "moderation", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction("Index", "AdminModeration");
        if (!string.IsNullOrEmpty(returnStatus))
            return RedirectToAction("Comments", new { status = returnStatus });
        return RedirectToAction("Index", "AdminModeration");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectComment([FromForm] int id, [FromForm] string? returnStatus, [FromForm] string? returnTo)
    {
        if (id <= 0) return BadRequest();

        var comment = await _db.Comments.AsTracking()
            .Include(c => c.Post)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (comment is null) return NotFound();

        if (!AuthorAccess.CanModerateComment(User, comment.Post))
            return Forbid();

        comment.Status = CommentStatus.Rejected;
        await _db.SaveChangesAsync();
        _broadcaster.Publish(new { type = "comment", status = "rejected", id = comment.Id });

        if (string.Equals(returnTo, "moderation", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction("Index", "AdminModeration");
        if (!string.IsNullOrEmpty(returnStatus))
            return RedirectToAction("Comments", new { status = returnStatus });
        return RedirectToAction("Index", "AdminModeration");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment([FromForm] int id, [FromForm] string? returnStatus, [FromForm] string? returnTo)
    {
        if (id <= 0) return BadRequest();

        var comment = await _db.Comments.AsTracking()
            .Include(c => c.Post)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (comment is null) return NotFound();

        if (!AuthorAccess.CanModerateComment(User, comment.Post))
            return Forbid();

        _db.Comments.Remove(comment);
        await _db.SaveChangesAsync();
        _broadcaster.Publish(new { type = "comment", status = "deleted", id = id });

        if (string.Equals(returnTo, "moderation", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction("Index", "AdminModeration");
        if (!string.IsNullOrEmpty(returnStatus))
            return RedirectToAction("Comments", new { status = returnStatus });
        return RedirectToAction("Index", "AdminModeration");
    }
}
