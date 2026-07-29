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
}
