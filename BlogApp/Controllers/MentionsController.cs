using BlogApp.Data;
using BlogApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

/// <summary>@mention typeahead (Telegram-style) + related endpoints.</summary>
[Route("Mentions")]
public class MentionsController : Controller
{
    private readonly ApplicationDbContext _db;

    public MentionsController(ApplicationDbContext db) => _db = db;

    /// <summary>
    /// GET /Mentions/Suggest?q=ali
    /// Returns up to 8 users whose username or display name starts with / contains q.
    /// </summary>
    [HttpGet("Suggest")]
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Suggest(string? q, CancellationToken ct)
    {
        q = (q ?? "").Trim();
        if (q.Length is < 1 or > 32)
            return Json(Array.Empty<object>());

        // strip leading @ if pasted
        if (q.StartsWith('@')) q = q[1..];
        if (q.Length < 1) return Json(Array.Empty<object>());

        var term = q.ToLowerInvariant();

        var users = await _db.Users.AsNoTracking()
            .Where(u => u.UserName != null
                        && (u.UserName.ToLower().StartsWith(term)
                            || (u.DisplayName != null && u.DisplayName.ToLower().Contains(term))))
            .OrderBy(u => u.UserName!.ToLower().StartsWith(term) ? 0 : 1)
            .ThenBy(u => u.UserName)
            .Take(8)
            .Select(u => new
            {
                id = u.Id,
                username = u.UserName,
                displayName = string.IsNullOrWhiteSpace(u.DisplayName) ? u.UserName : u.DisplayName,
                hasAvatar = u.ProfileImage != null && u.ProfileImage.Length > 0,
                avatarUrl = (u.ProfileImage != null && u.ProfileImage.Length > 0)
                    ? "/Account/ProfileImage?userId=" + u.Id
                    : (string?)null
            })
            .ToListAsync(ct);

        return Json(users);
    }
}
