using System.Text.Json;
using AVICRM.Api.Dtos;
using AVICRM.Api.Validation;
using AVICRM.Data;
using AVICRM.Models;
using Microsoft.EntityFrameworkCore;

namespace AVICRM.Services.Messaging;

/// <summary>Sequential write: create comment via topic bus.</summary>
public sealed class CommentCreateWorkHandler : IApiWorkHandler
{
    private readonly ApplicationDbContext _db;

    public CommentCreateWorkHandler(ApplicationDbContext db) => _db = db;

    public IEnumerable<string> Kinds { get; } = new[] { "comments.create" };

    public async Task<ApiWorkResult> HandleAsync(ApiWorkRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.PayloadJson))
            return Fail(request, 400, "empty_body");

        ApiCommentCreateDto? dto;
        try { dto = JsonSerializer.Deserialize<ApiCommentCreateDto>(request.PayloadJson); }
        catch { return Fail(request, 400, "invalid_json"); }
        if (dto is null) return Fail(request, 400, "invalid_json");

        var author = InputSanitizer.Clamp(dto.AuthorName, 80) ?? "API";
        var body = InputSanitizer.Clamp(dto.Body, 4000);
        if (string.IsNullOrWhiteSpace(body) || !InputSanitizer.IsSafePlainText(body))
            return Fail(request, 400, "invalid_body");

        var postExists = await _db.Posts.AsNoTracking()
            .AnyAsync(p => p.Id == dto.PostId && !p.IsDeleted && p.IsPublished, ct);
        if (!postExists) return Fail(request, 404, "post_not_found");

        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        var comment = new Comment
        {
            PostId = dto.PostId,
            AuthorName = author,
            Body = body!,
            Status = CommentStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Comments.Add(comment);
        await _db.SaveChangesAsync(ct);

        var payload = JsonSerializer.Serialize(new
        {
            id = comment.Id,
            postId = comment.PostId,
            authorName = comment.AuthorName,
            body = comment.Body,
            createdAtUtc = comment.CreatedAtUtc,
            likeCount = comment.LikeCount,
            status = comment.Status.ToString(),
            message = "queued_and_saved"
        });

        return new ApiWorkResult
        {
            CorrelationId = request.CorrelationId,
            Ok = true,
            StatusCode = 201,
            BodyJson = payload
        };
    }

    private static ApiWorkResult Fail(ApiWorkRequest r, int code, string err) => new()
    {
        CorrelationId = r.CorrelationId,
        Ok = false,
        StatusCode = code,
        Error = err
    };
}

/// <summary>Load-smoothed posts list: runs one-by-one under stress.</summary>
public sealed class PostsListWorkHandler : IApiWorkHandler
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;

    public PostsListWorkHandler(ApplicationDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public IEnumerable<string> Kinds { get; } = new[] { "posts.list" };

    public async Task<ApiWorkResult> HandleAsync(ApiWorkRequest request, CancellationToken ct)
    {
        var q = string.IsNullOrEmpty(request.PayloadJson)
            ? new Dictionary<string, string?>()
            : JsonSerializer.Deserialize<Dictionary<string, string?>>(request.PayloadJson)
              ?? new Dictionary<string, string?>();

        int.TryParse(q.GetValueOrDefault("page"), out var page);
        int.TryParse(q.GetValueOrDefault("pageSize"), out var pageSize);
        page = Math.Clamp(page <= 0 ? 1 : page, 1, 10_000);
        pageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 50);
        var lang = InputSanitizer.Clamp(q.GetValueOrDefault("lang"), 8) ?? "fa";
        var search = InputSanitizer.Clamp(q.GetValueOrDefault("q"), 100);
        var tag = InputSanitizer.Clamp(q.GetValueOrDefault("tag"), 80);
        var category = InputSanitizer.Clamp(q.GetValueOrDefault("category"), 80);

        var now = DateTime.UtcNow;
        var query = _db.Posts.AsNoTracking()
            .Where(p => !p.IsDeleted && p.IsPublished)
            .Where(p => p.LanguageCode == lang)
            .Where(p => p.ExpiresAtUtc == null || p.ExpiresAtUtc > now);

        if (!string.IsNullOrEmpty(tag))
            query = query.Where(p => p.PostTags.Any(pt => pt.Tag.Slug == tag));
        if (!string.IsNullOrEmpty(category))
            query = query.Where(p => p.Category != null && p.Category.Slug == category);
        if (!string.IsNullOrEmpty(search))
            query = query.Where(p => p.Title.Contains(search) || (p.Summary != null && p.Summary.Contains(search)));

        var total = await query.CountAsync(ct);
        var baseUrl = (_config["Seo:BaseUrl"] ?? "https://localhost").TrimEnd('/');

        var items = await query
            .OrderByDescending(p => p.PublishedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.Title,
                p.Slug,
                p.Summary,
                category = p.Category != null ? p.Category.Name : null,
                tags = p.PostTags.Select(pt => pt.Tag.Name).ToList(),
                p.PublishedAtUtc,
                p.ReadingTimeMinutes,
                p.LanguageCode,
                url = baseUrl + "/" + p.LanguageCode + "/post/" + p.Slug
            })
            .ToListAsync(ct);

        var body = JsonSerializer.Serialize(new { items, page, pageSize, total });
        return new ApiWorkResult
        {
            CorrelationId = request.CorrelationId,
            Ok = true,
            StatusCode = 200,
            BodyJson = body
        };
    }
}
