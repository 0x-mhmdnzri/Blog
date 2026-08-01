using System.Text.Json;
using AVICRM.Api.Auth;
using AVICRM.Api.Dtos;
using AVICRM.Api.Validation;
using AVICRM.Data;
using AVICRM.Models;
using AVICRM.Services.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace AVICRM.Api.Controllers;

[ApiController]
[Route("api/v1/posts")]
[EnableRateLimiting("api")]
[IgnoreAntiforgeryToken]
[Authorize(AuthenticationSchemes = ApiKeyDefaults.Scheme)]
public class PostsApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;
    private readonly IApiTopicBus _bus;

    public PostsApiController(ApplicationDbContext db, IConfiguration config, IApiTopicBus bus)
    {
        _db = db;
        _config = config;
        _bus = bus;
    }

    /// <summary>
    /// List posts — enqueued on topic bus and processed one-by-one (no miss under load).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? q = null,
        [FromQuery] string? lang = null,
        [FromQuery] string? tag = null,
        [FromQuery] string? category = null,
        CancellationToken ct = default)
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

        int? keyId = null;
        if (int.TryParse(User.FindFirst("api_key_id")?.Value, out var kid)) keyId = kid;

        var payload = JsonSerializer.Serialize(new Dictionary<string, string?>
        {
            ["page"] = page.ToString(),
            ["pageSize"] = pageSize.ToString(),
            ["q"] = q,
            ["lang"] = lang,
            ["tag"] = tag,
            ["category"] = category
        });

        var work = new ApiWorkRequest
        {
            Kind = "posts.list",
            Method = "GET",
            Path = "/api/v1/posts",
            UserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            ApiKeyId = keyId,
            PayloadJson = payload
        };

        var result = await _bus.EnqueueAndWaitAsync(work, ct: ct);
        if (!result.Ok)
            return StatusCode(result.StatusCode, new ApiErrorDto(result.Error ?? "work_failed"));

        return Content(result.BodyJson ?? "{}", "application/json");
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
