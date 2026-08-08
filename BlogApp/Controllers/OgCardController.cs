using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services.Seo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

/// <summary>
/// Open Graph / Twitter Card images for social crawlers
/// (LinkedIn, Telegram, WhatsApp, X, Facebook, Discord, Slack, …).
/// Absolute URLs are set in page meta; this endpoint returns 1200×630 cards.
/// </summary>
[AllowAnonymous]
[DisableRateLimiting]
[Route("og")]
public sealed class OgCardController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IPostOgCardService _cards;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ILogger<OgCardController> _log;

    public OgCardController(
        ApplicationDbContext db,
        IPostOgCardService cards,
        UserManager<ApplicationUser> users,
        ILogger<OgCardController> log)
    {
        _db = db;
        _cards = cards;
        _users = users;
        _log = log;
    }

    /// <summary>Post share card — title, summary, views, likes, read time, date, tags, category.</summary>
    [HttpGet("post/{id:int}.png")]
    [HttpGet("post/{id:int}")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "v", "force" })]
    public async Task<IActionResult> PostCardPng(int id, bool force = false, CancellationToken ct = default)
    {
        var post = await LoadPostAsync(id, ct);
        if (post is null)
        {
            _log.LogWarning("OG PNG 404: post {Id} missing or unpublished", id);
            return NotFound();
        }

        if (force)
            await _cards.InvalidatePostAsync(post.Id, ct);

        var png = await _cards.GetOrCreatePngAsync(post, ct);
        if (png is null || png.Length == 0)
        {
            _log.LogWarning("OG PNG render empty for post {Id} — serving fallback", id);
            png = _cards.CreateFallbackPng(post.Title ?? $"Post {id}");
        }

        ApplyCrawlerHeaders(force ? "no-store" : "public, max-age=86400, stale-while-revalidate=604800");
        return File(png, "image/png");
    }

    /// <summary>JPEG variant — some WhatsApp / older crawlers prefer JPEG.</summary>
    [HttpGet("post/{id:int}.jpg")]
    [HttpGet("post/{id:int}.jpeg")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "v", "force" })]
    public async Task<IActionResult> PostCardJpeg(int id, bool force = false, CancellationToken ct = default)
    {
        var post = await LoadPostAsync(id, ct);
        if (post is null)
        {
            _log.LogWarning("OG JPEG 404: post {Id} missing or unpublished", id);
            return NotFound();
        }

        if (force)
            await _cards.InvalidatePostAsync(post.Id, ct);

        var jpg = await _cards.GetOrCreateJpegAsync(post, ct);
        if (jpg is null || jpg.Length == 0)
        {
            _log.LogWarning("OG JPEG render empty for post {Id} — serving fallback", id);
            jpg = _cards.CreateFallbackJpeg(post.Title ?? $"Post {id}");
        }

        ApplyCrawlerHeaders(force ? "no-store" : "public, max-age=86400, stale-while-revalidate=604800");
        return File(jpg, "image/jpeg");
    }

    /// <summary>Default site OG card for home / generic pages.</summary>
    [HttpGet("site.png")]
    [HttpGet("site")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "v" })]
    public async Task<IActionResult> SiteCard(CancellationToken ct)
    {
        var png = await _cards.GetOrCreateSitePngAsync(ct);
        if (png is null || png.Length == 0)
            png = _cards.CreateFallbackPng("Blog");

        ApplyCrawlerHeaders("public, max-age=86400, stale-while-revalidate=604800");
        return File(png!, "image/png");
    }

    /// <summary>Author profile share card — posts, followers, total views.</summary>
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
        if (png is null || png.Length == 0)
            png = _cards.CreateFallbackPng(user.DisplayName ?? "Author");

        ApplyCrawlerHeaders("public, max-age=3600, stale-while-revalidate=86400");
        return File(png!, "image/png");
    }

    private async Task<Post?> LoadPostAsync(int id, CancellationToken ct)
    {
        return await _db.Posts.AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.Category)
            .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted && p.IsPublished, ct);
    }

    private void ApplyCrawlerHeaders(string cacheControl)
    {
        Response.Headers.CacheControl = cacheControl;
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.Remove("X-Frame-Options");
        Response.Headers["Cross-Origin-Resource-Policy"] = "cross-origin";
    }
}
