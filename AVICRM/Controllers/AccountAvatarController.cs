using AVICRM.Data;
using AVICRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AVICRM.Controllers;

/// <summary>Serves profile images for avatars (admin menu + public).</summary>
[Authorize]
public class AccountAvatarController : Controller
{
    private readonly ApplicationDbContext _db;

    public AccountAvatarController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> Me()
    {
        var uid = AuthorAccess.UserId(User);
        if (uid is null) return NotFound();
        var user = await _db.Users.AsNoTracking()
            .Where(u => u.Id == uid)
            .Select(u => new { u.ProfileImage, u.ProfileImageContentType, u.DisplayName, u.UserName })
            .FirstOrDefaultAsync();
        if (user?.ProfileImage is { Length: > 0 })
            return File(user.ProfileImage, user.ProfileImageContentType ?? "image/jpeg");

        // SVG placeholder with initial
        var initial = (user?.DisplayName ?? user?.UserName ?? "?");
        if (string.IsNullOrWhiteSpace(initial)) initial = "?";
        initial = char.ToUpperInvariant(initial.Trim()[0]).ToString();
        var svg =
            $"<svg xmlns='http://www.w3.org/2000/svg' width='96' height='96' viewBox='0 0 96 96'>" +
            $"<rect width='96' height='96' rx='48' fill='%2312161f'/>" +
            $"<text x='50%' y='54%' dominant-baseline='middle' text-anchor='middle' " +
            $"font-family='system-ui,sans-serif' font-size='40' font-weight='600' fill='%23e3b341'>{initial}</text></svg>";
        return Content(svg, "image/svg+xml; charset=utf-8");
    }
}
