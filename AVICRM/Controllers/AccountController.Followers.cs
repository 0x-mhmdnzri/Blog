using AVICRM.Models;
using AVICRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AVICRM.Controllers;

public partial class AccountController
{
    /// <summary>
    /// Author (or SuperAdmin) panel: list followers of the current author,
    /// or of a specified author when SuperAdmin passes authorUserId.
    /// </summary>
    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpGet]
    public async Task<IActionResult> Followers(string? authorUserId = null)
    {
        var selfId = AuthorAccess.UserId(User)!;
        var isSuper = AuthorAccess.IsSuperAdmin(User);

        var targetId = selfId;
        if (!string.IsNullOrWhiteSpace(authorUserId) && isSuper)
            targetId = authorUserId;

        var author = await _userManager.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == targetId);
        if (author is null) return NotFound();

        // Non-super authors can only view their own followers
        if (!isSuper && author.Id != selfId)
            return Forbid();

        var followers = await _db.AuthorFollows.AsNoTracking()
            .Where(f => f.AuthorUserId == targetId)
            .OrderByDescending(f => f.CreatedAtUtc)
            .Join(_db.Users.AsNoTracking(),
                f => f.FollowerUserId,
                u => u.Id,
                (f, u) => new FollowerRow
                {
                    UserId = u.Id,
                    UserName = u.UserName ?? "",
                    DisplayName = u.DisplayName,
                    Email = u.Email,
                    FollowedAtUtc = f.CreatedAtUtc
                })
            .ToListAsync();

        var following = await _db.AuthorFollows.AsNoTracking()
            .Where(f => f.FollowerUserId == targetId)
            .OrderByDescending(f => f.CreatedAtUtc)
            .Join(_db.Users.AsNoTracking(),
                f => f.AuthorUserId,
                u => u.Id,
                (f, u) => new FollowerRow
                {
                    UserId = u.Id,
                    UserName = u.UserName ?? "",
                    DisplayName = u.DisplayName,
                    Email = u.Email,
                    FollowedAtUtc = f.CreatedAtUtc
                })
            .ToListAsync();

        ViewData["Title"] = "دنبال‌کنندگان";
        ViewBag.Author = author;
        ViewBag.Followers = followers;
        ViewBag.Following = following;
        ViewBag.IsOwn = author.Id == selfId;
        return View();
    }

    public sealed class FollowerRow
    {
        public string UserId { get; set; } = "";
        public string UserName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string? Email { get; set; }
        public DateTime FollowedAtUtc { get; set; }
    }
}
