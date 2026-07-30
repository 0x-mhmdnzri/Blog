using System.Text.Json;
using BlogApp.Api.Auth;
using BlogApp.Api.Validation;
using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Api.Controllers;

/// <summary>
/// Minimal GraphQL-compatible endpoint (posts query) without heavy runtime deps.
/// Supports: { posts(limit: N) { id title slug summary } } and { post(slug: "...") { ... } }
/// </summary>
[ApiController]
[Route("api/graphql")]
[EnableRateLimiting("api")]
[Authorize(AuthenticationSchemes = ApiKeyDefaults.Scheme)]
public class GraphQlController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly SeoService _seo;

    public GraphQlController(ApplicationDbContext db, SeoService seo)
    {
        _db = db;
        _seo = seo;
    }

    public record GqlRequest(string? Query, Dictionary<string, JsonElement>? Variables);

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] GqlRequest body)
    {
        var scopes = User.FindFirst("api_scopes")?.Value ?? "";
        if (!ApiScopes.Has(scopes, ApiScopes.Read))
            return Forbid();

        var query = body.Query?.Trim() ?? "";
        if (query.Length is 0 or > 4000)
            return BadRequest(new { errors = new[] { new { message = "Invalid query" } } });

        // Reject introspection amplification / injection markers
        if (query.Contains("__schema", StringComparison.OrdinalIgnoreCase)
            || query.Contains("mutation", StringComparison.OrdinalIgnoreCase)
            || query.Contains("<", StringComparison.Ordinal))
            return BadRequest(new { errors = new[] { new { message = "Query not allowed" } } });

        try
        {
            if (query.Contains("posts", StringComparison.OrdinalIgnoreCase)
                && !query.Contains("post(", StringComparison.OrdinalIgnoreCase))
            {
                var limit = ExtractIntArg(query, "limit", 10);
                limit = Math.Clamp(limit, 1, 50);
                var baseUrl = _seo.BaseUrl.TrimEnd('/');
                var items = await _db.Posts.AsNoTracking()
                    .Where(p => !p.IsDeleted && p.IsPublished)
                    .OrderByDescending(p => p.PublishedAtUtc)
                    .Take(limit)
                    .Select(p => new
                    {
                        id = p.Id,
                        title = p.Title,
                        slug = p.Slug,
                        summary = p.Summary,
                        publishedAtUtc = p.PublishedAtUtc,
                        url = baseUrl + "/" + p.LanguageCode + "/post/" + p.Slug
                    })
                    .ToListAsync();
                return Ok(new { data = new { posts = items } });
            }

            if (query.Contains("post(", StringComparison.OrdinalIgnoreCase))
            {
                var slug = ExtractStringArg(query, "slug");
                slug = InputSanitizer.Clamp(slug, 200);
                if (string.IsNullOrEmpty(slug) || !InputSanitizer.IsSafePlainText(slug))
                    return BadRequest(new { errors = new[] { new { message = "Invalid slug" } } });

                var p = await _db.Posts.AsNoTracking()
                    .Where(x => !x.IsDeleted && x.IsPublished && x.Slug == slug)
                    .Select(x => new
                    {
                        id = x.Id,
                        title = x.Title,
                        slug = x.Slug,
                        summary = x.Summary,
                        contentMarkdown = x.ContentMarkdown,
                        publishedAtUtc = x.PublishedAtUtc
                    })
                    .FirstOrDefaultAsync();

                if (p is null)
                    return Ok(new { data = new { post = (object?)null } });

                return Ok(new { data = new { post = p } });
            }

            return BadRequest(new { errors = new[] { new { message = "Unsupported query. Use posts or post(slug)." } } });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { errors = new[] { new { message = "Server error", detail = ex.Message } } });
        }
    }

    private static int ExtractIntArg(string query, string name, int fallback)
    {
        var marker = name + ":";
        var i = query.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return fallback;
        var slice = query[(i + marker.Length)..];
        var num = new string(slice.TakeWhile(c => char.IsDigit(c) || c == ' ').ToArray()).Trim();
        return int.TryParse(num, out var n) ? n : fallback;
    }

    private static string ExtractStringArg(string query, string name)
    {
        var marker = name + ":";
        var i = query.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return "";
        var slice = query[(i + marker.Length)..].TrimStart();
        if (slice.Length == 0) return "";
        var quote = slice[0];
        if (quote is not ('"' or '\'')) return "";
        var end = slice.IndexOf(quote, 1);
        return end > 1 ? slice[1..end] : "";
    }
}
