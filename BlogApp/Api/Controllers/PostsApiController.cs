using BlogApp.Api.Auth;
using BlogApp.Api.Dtos;
using BlogApp.Api.Validation;
using BlogApp.Data;
using BlogApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Api.Controllers;

[ApiController]
[Route("api/v1/posts")]
[EnableRateLimiting("api")]
[IgnoreAntiforgeryToken]
[Authorize(AuthenticationSchemes = ApiKeyDefaults.Scheme)]
public class PostsApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;

    public PostsApiController(ApplicationDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResultDto<ApiPostListItemDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? q = null,
        [FromQuery] string? lang = null,
        [FromQuery] string? tag = null,
        [FromQuery] string? category = null)
    {
        if (!HasScope(ApiScopes.Read)) return Forbid();

        page = Math.Clamp(page, 1, 10_000);
        pageSize = Math.Clamp(pageSize, 1, 50);
        lang = InputSanitizer.Clamp(lang, 8);
        if (string.IsNullOrWhiteSpace(lang)) lang = "fa";

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = InputSanitizer.Clamp(q.Trim(), 100);
            if (!InputSanitizer.IsSafePlainText(q))
                return BadRequest(new ApiErrorDto("Invalid query"));
        }

        tag = string.IsNullOrWhiteSpace(tag) ? null : InputSanitizer.Clamp(tag, 80);
        category = string.IsNullOrWhiteSpace(category) ? null : InputSanitizer.Clamp(category, 80);

        var now = DateTime.UtcNow;
        var query = _db.Posts.AsNoTracking()
            .Where(p => !p.IsDeleted && p.IsPublished)
            .Where(p => p.LanguageCode == lang)
            .Where(p => p.ExpiresAtUtc == null || p.ExpiresAtUtc > now)
            .Where(p => p.TranslationStatus == TranslationStatus.Original
                        || p.TranslationStatus == TranslationStatus.Approved);

        if (!string.IsNullOrEmpty(tag))
            query = query.Where(p => p.PostTags.Any(pt => pt.Tag.Slug == tag));
        if (!string.IsNullOrEmpty(category))
            query = query.Where(p => p.Category != null && p.Category.Slug == category);
        if (!string.IsNullOrEmpty(q))
            query = query.Where(p => p.Title.Contains(q) || (p.Summary != null && p.Summary.Contains(q)));

        var total = await query.CountAsync();
        var baseUrl = (_config["Seo:BaseUrl"] ?? $"{Request.Scheme}://{Request.Host}").TrimEnd('/');

        var items = await query
            .OrderByDescending(p => p.PublishedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ApiPostListItemDto(
                p.Id,
                p.Title,
                p.Slug,
                p.Summary,
                p.Category != null ? p.Category.Name : null,
                p.PostTags.Select(pt => pt.Tag.Name).ToList(),
                p.PublishedAtUtc,
                p.ReadingTimeMinutes,
                p.LanguageCode,
                baseUrl + "/" + p.LanguageCode + "/post/" + p.Slug))
            .ToListAsync();

        return Ok(new PagedResultDto<ApiPostListItemDto>(items, page, pageSize, total));
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult<ApiPostDetailDto>> Get(string slug, [FromQuery] string? lang = null)
    {
        if (!HasScope(ApiScopes.Read)) return Forbid();

        slug = InputSanitizer.Clamp(slug, 200);
        lang = string.IsNullOrWhiteSpace(lang) ? null : InputSanitizer.Clamp(lang, 8);

        var q = _db.Posts.AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Author)
            .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
            .Where(p => !p.IsDeleted && p.IsPublished && p.Slug == slug);

        if (!string.IsNullOrEmpty(lang))
            q = q.Where(p => p.LanguageCode == lang);

        var p = await q.FirstOrDefaultAsync();
        if (p is null) return NotFound(new ApiErrorDto("Post not found"));

        var baseUrl = (_config["Seo:BaseUrl"] ?? $"{Request.Scheme}://{Request.Host}").TrimEnd('/');
        return Ok(new ApiPostDetailDto(
            p.Id, p.Title, p.Slug, p.Summary, p.ContentMarkdown,
            p.Category?.Name,
            p.PostTags.Select(pt => pt.Tag.Name).ToList(),
            p.PublishedAtUtc, p.ReadingTimeMinutes, p.ViewCount, p.LikeCount,
            p.LanguageCode, p.Author?.UserName,
            baseUrl + "/" + p.LanguageCode + "/post/" + p.Slug));
    }

    private bool HasScope(string scope)
    {
        var scopes = User.FindFirst("api_scopes")?.Value ?? "";
        return ApiScopes.Has(scopes, scope);
    }
}
