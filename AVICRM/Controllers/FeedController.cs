using AVICRM.Data;
using AVICRM.Models;
using AVICRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AVICRM.Controllers;

[Authorize(Roles = AppRoles.Reader + "," + AppRoles.Author + "," + AppRoles.SuperAdmin)]
public class FeedController : Controller
{
    private readonly ApplicationDbContext _db;

    public FeedController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1)
    {
        const int pageSize = 30;
        if (page < 1) page = 1;
        var userId = AuthorAccess.UserId(User)!;

        var followingAuthorIds = await _db.AuthorFollows.AsNoTracking()
            .Where(f => f.FollowerUserId == userId)
            .Select(f => f.AuthorUserId)
            .ToListAsync();

        var followingCategoryIds = await _db.CategoryFollows.AsNoTracking()
            .Where(f => f.UserId == userId)
            .Select(f => f.CategoryId)
            .ToListAsync();

        var query = _db.UserActivities.AsNoTracking()
            .Where(a => a.ActorUserId == userId
                        || followingAuthorIds.Contains(a.ActorUserId)
                        || (a.TargetUserId != null && a.TargetUserId == userId)
                        || (a.CategoryId != null && followingCategoryIds.Contains(a.CategoryId.Value)))
            .OrderByDescending(a => a.CreatedAtUtc);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var actorIds = items.Select(i => i.ActorUserId).Distinct().ToList();
        var actors = await _db.Users.AsNoTracking()
            .Where(u => actorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName ?? u.UserName ?? "?");

        ViewData["Title"] = "فعالیت‌ها";
        ViewBag.Page = page;
        ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        ViewBag.Actors = actors;
        return View(items);
    }
}
