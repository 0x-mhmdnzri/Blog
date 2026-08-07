using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public partial class AccountController
{
    [HttpGet]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> PublicProfile(
        string userName,
        string? q = null,
        string? sort = null,
        string? folder = null,
        string? category = null,
        string? tag = null,
        string? series = null,
        string? topic = null,
        int? year = null)
    {
        if (string.IsNullOrWhiteSpace(userName) || userName.Length > 64)
            return NotFound();

        var user = await _userManager.FindByNameAsync(userName);
        if (user is null) return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        var isAuthor = roles.Contains(AppRoles.Author) || roles.Contains(AppRoles.SuperAdmin);
        var isSuper = roles.Contains(AppRoles.SuperAdmin);

        var baseQuery = _db.Posts.AsNoTracking()
            .Where(p => p.AuthorId == user.Id && p.IsPublished && !p.IsDeleted);

        var postCount = await baseQuery.CountAsync();
        var totalViews = await baseQuery.SumAsync(p => (long)p.ViewCount);
        var followerCount = await _db.AuthorFollows.CountAsync(f => f.AuthorUserId == user.Id);

        // NOTE: full body restored from pre-PLACEHOLDER + ProfileUsername — see local RESTORE if incomplete
        ViewData["Title"] = postCount > 0 ? $"{(string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName)} — {postCount} posts" : (user.DisplayName ?? user.UserName);
        ViewData["OgType"] = "profile";
        ViewData["ProfileUsername"] = user.UserName;
        ViewData["Author"] = string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName;
        ViewData["NoIndex"] = false;

        return NotFound(); // TEMP - DO NOT USE - full restore needed
    }
}
