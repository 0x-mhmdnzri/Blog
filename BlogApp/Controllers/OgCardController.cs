using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services.Seo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

[AllowAnonymous]
[Route("og")]
public sealed class OgCardController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IPostOgCardService _cards;
    private readonly UserManager<ApplicationUser> _users;

    public OgCardController(
        ApplicationDbContext db,
        IPostOgCardService cards,
        UserManager<ApplicationUser> users)
    {
        _db = db;
        _cards = cards;
        _users = users;
    }

    [HttpGet("post/{id:int}.png")]
    [HttpGet("post/{id:int}")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "v" })]
    public async Task<IActionResult> PostCard(int id, CancellationToken ct)
    {
        var post = await _db.Posts.AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted && p.IsPublished, ct);
        if (post is null) return NotFound();

        var png = await _cards.GetOrCreatePngAsync(post, ct);
        if (png is null || png.Length == 0) return NotFound();

        Response.Headers.CacheControl = "public, max-age=86400";
        return File(png, "image/png");
    }

    [HttpGet("site.png")]
    [HttpGet("site")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "v" })]
    public async Task<IActionResult> SiteCard(CancellationToken ct)
    {
        var png = await _cards.GetOrCreateSitePngAsync(ct);
        if (png is null || png.Length == 0) return NotFound();
        Response.Headers.CacheControl = "public, max-age=86400";
        return File(png, "image/png");
    }

    [HttpGet("author/{userId}.png")]
    [HttpGet("author/{userId}")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "v" })]
    public async Task<IActionResult> AuthorCard(string userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId)) return NotFound();
        var user = await _users.FindByIdAsync(userId);
        if (user is null) return NotFound();

        var postCount = await _db.Posts.AsNoTracking()
            .CountAsync(p => p.AuthorId == user.Id && p.IsPublished && !p.IsDeleted, ct);
        var totalViews = await _db.Posts.AsNoTracking()
            .Where(p => p.AuthorId == user.Id && p.IsPublished && !p.IsDeleted)
            .SumAsync(p => (long)p.ViewCount, ct);
        var followers = await _db.AuthorFollows.AsNoTracking()
            .CountAsync(f => f.AuthorUserId == user.Id, ct);

        var png = await _cards.GetOrCreateAuthorPngAsync(
            user.Id,
            user.DisplayName ?? user.UserName ?? "Author",
            user.UserName ?? "",
            user.Bio,
            postCount,
            followers,
            totalViews,
            ct);
        if (png is null || png.Length == 0) return NotFound();

        Response.Headers.CacheControl = "public, max-age=3600";
        return File(png, "image/png");
    }
}
