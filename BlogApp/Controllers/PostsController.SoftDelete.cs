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

        var actorName = User.Identity?.Name ?? actorId ?? "?";
        try
        {
            var um = HttpContext.RequestServices.GetService<UserManager<ApplicationUser>>();
            if (um is not null && actorId is not null)
            {
                var me = await um.FindByIdAsync(actorId);
                if (me is not null)
                    actorName = string.IsNullOrWhiteSpace(me.DisplayName) ? (me.UserName ?? actorName) : me.DisplayName;
            }
        }
        catch { /* ignore */ }

        _logger.LogInformation(
            "Post soft-deleted PostId={Id} Title={Title} By={UserId} Name={Name} Super={IsSuper} Reason={Reason}",
            post.Id, post.Title, actorId, actorName, isSuper, note ?? "");

        try
        {
            await NotifyPostDeletedAsync(post, isSuper, note, actorName, actorId);
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

    /// <summary>
    /// Always inform SuperAdmins: who deleted which post and why.
    /// If SuperAdmin deleted another author's post, also notify that author.
    /// </summary>
    private async Task NotifyPostDeletedAsync(
        Post post,
        bool deletedBySuperAdmin,
        string? reason,
        string actorDisplayName,
        string? actorId)
    {
        var why = string.IsNullOrWhiteSpace(reason) ? "بدون دلیل ذکر شده" : reason.Trim();
        var roleLabel = deletedBySuperAdmin ? "سوپر ادمین" : "نویسنده";
        var bodySuper =
            roleLabel + " «" + actorDisplayName + "» نوشته «" + post.Title + "» (#" + post.Id + ") را حذف کرد.\nدلیل: " + why;

        var userManager = HttpContext.RequestServices.GetService<UserManager<ApplicationUser>>();
        if (userManager is not null)
        {
            var supers = await userManager.GetUsersInRoleAsync(AppRoles.SuperAdmin);
            foreach (var s in supers)
            {
                if (string.IsNullOrEmpty(s.Id)) continue;
                if (actorId is not null && string.Equals(s.Id, actorId, StringComparison.Ordinal))
                    continue;
                await _notify.NotifyAsync(
                    s.Id,
                    NotificationKind.AdminMessage,
                    "حذف نوشته",
                    bodySuper,
                    "/Admin/Posts");
            }
        }

        if (deletedBySuperAdmin
            && !string.IsNullOrEmpty(post.AuthorId)
            && !string.Equals(post.AuthorId, actorId, StringComparison.Ordinal))
        {
            var bodyAuthor = string.IsNullOrWhiteSpace(reason)
                ? "«" + post.Title + "» توسط سوپر ادمین (" + actorDisplayName + ") حذف شد."
                : "«" + post.Title + "» توسط سوپر ادمین (" + actorDisplayName + ") حذف شد. دلیل: " + why;

            await _notify.NotifyAsync(
                post.AuthorId,
                NotificationKind.AdminMessage,
                "نوشته شما حذف شد",
                bodyAuthor,
                "/Admin/Posts");
        }
    }
}
