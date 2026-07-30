using System.Text.Json;
using BlogApp.Api.Auth;
using BlogApp.Api.Validation;
using BlogApp.Data;
using BlogApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Api.Controllers;

[ApiController]
[Route("api/graphql")]
[EnableRateLimiting("api")]
[IgnoreAntiforgeryToken]
[Authorize(AuthenticationSchemes = ApiKeyDefaults.Scheme)]
public class GraphQlController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;

    public GraphQlController(ApplicationDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
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

        if (query.Contains("__schema", StringComparison.OrdinalIgnoreCase)
            || query.Contains("mutation", StringComparison.OrdinalIgnoreCase)
            || query.Contains('<'))
            return BadRequest(new { errors = new[] { new { message = "Query not allowed" } } });

        var baseUrl = (_config["Seo:BaseUrl"] ?? $"{Request.Scheme}://{Request.Host}").TrimEnd('/');

        if (query.Contains("posts", StringComparison.OrdinalIgnoreCase)
            && !query.Contains("post(", StringComparison.OrdinalIgnoreCase))
        {
            var limit = ExtractIntArg(query, "limit", 10);
            limit = Math.Clamp(limit, 1, 50);
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
            var slug = InputSanitizer.Clamp(ExtractStringArg(query, "slug"), 200);
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

            return Ok(new { data = new { post = p } });
        }

        return BadRequest(new { errors = new[] { new { message = "Unsupported query. Use posts or post(slug)." } } });
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
