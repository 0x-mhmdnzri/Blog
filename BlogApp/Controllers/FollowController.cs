using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services;
using BlogApp.Services.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

[Authorize(Roles = AppRoles.Reader + "," + AppRoles.Author + "," + AppRoles.SuperAdmin)]
public class FollowController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly INotificationService _notify;

    public FollowController(ApplicationDbContext db, UserManager<ApplicationUser> users, INotificationService notify)
    {
        _db = db;
        _users = users;
        _notify = notify;
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(string authorUserId, string? returnUrl = null)
    {
        var followerId = AuthorAccess.UserId(User)!;
        if (string.IsNullOrEmpty(authorUserId) || authorUserId == followerId)
            return BadRequest();

        var author = await _users.FindByIdAsync(authorUserId);
        if (author is null) return NotFound();

        var existing = await _db.AuthorFollows
            .FirstOrDefaultAsync(f => f.FollowerUserId == followerId && f.AuthorUserId == authorUserId);

        if (existing is null)
        {
            _db.AuthorFollows.Add(new AuthorFollow
            {
                FollowerUserId = followerId,
                AuthorUserId = authorUserId,
                CreatedAtUtc = DateTime.UtcNow
            });
            ActivityWriter.Write(_db, followerId, ActivityKind.AuthorFollowed,
                targetUserId: authorUserId,
                title: author.DisplayName ?? author.UserName,
                linkUrl: $"/author/{author.UserName}");
            await _db.SaveChangesAsync();

            var follower = await _users.GetUserAsync(User);
            if (follower is not null)
                await _notify.NotifyNewFollowerAsync(authorUserId, follower);
        }
        else
        {
            _db.AuthorFollows.Remove(existing);
            await _db.SaveChangesAsync();
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("PublicProfile", "Account", new { userName = author.UserName });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCategory(int categoryId, string? returnUrl = null)
    {
        var userId = AuthorAccess.UserId(User)!;
        var cat = await _db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == categoryId);
        if (cat is null) return NotFound();

        var existing = await _db.CategoryFollows
            .FirstOrDefaultAsync(f => f.CategoryId == categoryId && f.UserId == userId);

        if (existing is null)
        {
            _db.CategoryFollows.Add(new CategoryFollow
            {
                CategoryId = categoryId,
                UserId = userId,
                CreatedAtUtc = DateTime.UtcNow
            });
            ActivityWriter.Write(_db, userId, ActivityKind.CategoryFollowed,
                categoryId: categoryId, title: cat.Name, linkUrl: $"/?category={cat.Slug}");
        }
        else
        {
            _db.CategoryFollows.Remove(existing);
        }

        await _db.SaveChangesAsync();

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Index", "Home", new { category = cat.Slug });
    }
}
