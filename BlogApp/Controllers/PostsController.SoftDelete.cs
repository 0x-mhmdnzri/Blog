using BlogApp.Models;
using BlogApp.Services;
using BlogApp.Services.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public partial class PostsController
{
    /// <summary>Soft-delete (trash). Authors: own posts only. SuperAdmin: any post.</summary>
    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? reason = null)
    {
        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;

        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == id);
        if (post is null) return NotFound();
        if (post.IsDeleted)
        {
            TempData["FlashOk"] = "این نوشته از قبل در سطل زباله است.";
            return RedirectToAction("Posts", "Admin");
        }
        if (!AuthorAccess.OwnsPost(User, post)) return Forbid();

        var actorId = AuthorAccess.UserId(User);
        var isSuper = AuthorAccess.IsSuperAdmin(User);
        var note = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (note is { Length: > 500 }) note = note[..500];

        post.IsDeleted = true;
        post.DeletedAtUtc = DateTime.UtcNow;
        post.IsPublished = false;
        post.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Post soft-deleted PostId={Id} Title={Title} By={UserId} Super={IsSuper}",
            post.Id, post.Title, actorId, isSuper);

        try
        {
            await NotifyPostDeletedAsync(post, isSuper, note);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notify after soft-delete failed PostId={Id}", post.Id);
        }

        TempData["FlashOk"] = "نوشته به سطل زباله منتقل شد.";
        return RedirectToAction("Posts", "Admin");
    }

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id)
    {
        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;

        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == id);
        if (post is null) return NotFound();
        if (!AuthorAccess.OwnsPost(User, post)) return Forbid();

        post.IsDeleted = false;
        post.DeletedAtUtc = null;
        post.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["FlashOk"] = "نوشته از سطل زباله بازگردانی شد.";
        return RedirectToAction("Posts", "Admin");
    }

    private async Task NotifyPostDeletedAsync(Post post, bool deletedBySuperAdmin, string? reason)
    {
        if (deletedBySuperAdmin)
        {
            var actorId = AuthorAccess.UserId(User);
            if (string.IsNullOrEmpty(post.AuthorId)) return;
            if (string.Equals(post.AuthorId, actorId, StringComparison.Ordinal)) return;

            var body = string.IsNullOrEmpty(reason)
                ? "«" + post.Title + "» توسط سوپر ادمین حذف شد."
                : "«" + post.Title + "» توسط سوپر ادمین حذف شد. دلیل: " + reason;

            await _notify.NotifyAsync(
                post.AuthorId,
                NotificationKind.AdminMessage,
                "نوشته شما حذف شد",
                body,
                "/Admin/Posts");
            return;
        }

        var userManager = HttpContext.RequestServices.GetService<UserManager<ApplicationUser>>();
        if (userManager is null) return;

        var supers = await userManager.GetUsersInRoleAsync(AppRoles.SuperAdmin);
        var authorName = User.Identity?.Name ?? post.AuthorId;
        var bodyAuthor = string.IsNullOrEmpty(reason)
            ? "نویسنده «" + authorName + "» نوشته «" + post.Title + "» را حذف کرد."
            : "نویسنده «" + authorName + "» نوشته «" + post.Title + "» را حذف کرد. دلیل: " + reason;

        foreach (var s in supers)
        {
            if (string.IsNullOrEmpty(s.Id)) continue;
            await _notify.NotifyAsync(
                s.Id,
                NotificationKind.AdminMessage,
                "حذف نوشته توسط نویسنده",
                bodyAuthor,
                "/Admin/Posts");
        }
    }
}
