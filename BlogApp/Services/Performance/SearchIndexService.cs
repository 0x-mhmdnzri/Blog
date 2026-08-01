using System.Data;
using System.Text.RegularExpressions;
using BlogApp.Data;
using BlogApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Services.Performance;

/// <summary>
/// Full-text index for public search (Spotlight-style).
/// Keeps SearchIndexEntries + SQLite FTS5 PostsFts in sync.
/// unicode61 tokenizer supports Persian, English, and mixed scripts.
/// </summary>
public sealed class SearchIndexService
{
    private static readonly Regex MdNoise = new(@"[#*_`>~\[\]()!]|\{+[^}]*\}+", RegexOptions.Compiled);
    // Verbatim string: "" = one double-quote character
    private static readonly Regex FtsSpecial = new(@"[""*^(){}\[\]:\\]", RegexOptions.Compiled);

    private readonly ApplicationDbContext _db;
    private readonly ILogger<SearchIndexService> _logger;

    public SearchIndexService(ApplicationDbContext db, ILogger<SearchIndexService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task IndexPostAsync(int postId, CancellationToken ct = default)
    {
        var post = await _db.Posts
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
            .FirstOrDefaultAsync(p => p.Id == postId, ct);

        if (post is null || post.IsDeleted || !post.IsPublished)
        {
            await RemovePostAsync(postId, ct);
            return;
        }

        string? authorName = null;
        string? authorUserId = post.AuthorId;
        if (!string.IsNullOrEmpty(post.AuthorId))
        {
            authorName = await _db.Users.AsNoTracking()
                .Where(u => u.Id == post.AuthorId)
                .Select(u => u.DisplayName ?? u.UserName)
                .FirstOrDefaultAsync(ct);
        }

        var body = StripMarkdown(post.ContentMarkdown);
        if (body.Length > 50_000) body = body[..50_000];

        var tags = string.Join(',', post.PostTags.Select(t => t.Tag.Name));
        var category = post.Category?.Name;

        var existing = await _db.SearchIndexEntries.AsTracking()
            .FirstOrDefaultAsync(s => s.PostId == postId, ct);

        if (existing is null)
        {
            existing = new SearchIndexEntry { PostId = postId };
            _db.SearchIndexEntries.Add(existing);
        }

        existing.LanguageCode = post.LanguageCode ?? "fa";
        existing.Title = post.Title;
        existing.Slug = post.Slug;
        existing.Summary = post.Summary;
        existing.BodyText = body;
        existing.TagsCsv = tags;
        existing.CategoryName = category;
        existing.AuthorUserId = authorUserId;
        existing.AuthorName = authorName;
        existing.IsPublished = post.IsPublished;
        existing.PublishedAtUtc = post.PublishedAtUtc;
        existing.IndexedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await UpsertFtsAsync(postId, post.Title, post.Summary, body, tags, category, authorName, existing.LanguageCode, ct);
        _logger.LogDebug("Search index updated PostId={Id}", postId);
    }

    public async Task RemovePostAsync(int postId, CancellationToken ct = default)
    {
        var rows = await _db.SearchIndexEntries.AsTracking().Where(s => s.PostId == postId).ToListAsync(ct);
        if (rows.Count > 0)
        {
            _db.SearchIndexEntries.RemoveRange(rows);
            await _db.SaveChangesAsync(ct);
        }

        try
        {
            await _db.Database.ExecuteSqlRawAsync("DELETE FROM PostsFts WHERE PostId = {0}", postId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PostsFts delete failed PostId={Id}", postId);
        }
    }

    public async Task<List<int>> SearchPostIdsAsync(string query, int take = 50, CancellationToken ct = default)
    {
        var match = BuildMatchQuery(query);
        if (match is null) return new List<int>();

        try
        {
            var conn = _db.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT PostId FROM PostsFts WHERE PostsFts MATCH $q ORDER BY bm25(PostsFts) LIMIT $take";
            var pQ = cmd.CreateParameter();
            pQ.ParameterName = "$q";
            pQ.Value = match;
            cmd.Parameters.Add(pQ);
            var pT = cmd.CreateParameter();
            pT.ParameterName = "$take";
            pT.Value = take;
            cmd.Parameters.Add(pT);

            var ids = new List<int>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                ids.Add(reader.GetInt32(0));
            return ids;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FTS MATCH failed; falling back to LIKE. q={Q}", query);
            return await FallbackLikeIdsAsync(query, take, ct);
        }
    }

    public async Task<List<SearchHit>> SearchHitsAsync(string query, int take = 12, CancellationToken ct = default)
    {
        var ids = await SearchPostIdsAsync(query, take, ct);
        if (ids.Count == 0) return new List<SearchHit>();

        var entries = await _db.SearchIndexEntries.AsNoTracking()
            .Where(s => ids.Contains(s.PostId) && s.IsPublished)
            .ToListAsync(ct);

        var order = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        return entries
            .OrderBy(e => order.GetValueOrDefault(e.PostId, int.MaxValue))
            .Select(e => new SearchHit
            {
                PostId = e.PostId,
                Title = e.Title,
                Slug = e.Slug,
                Summary = e.Summary,
                LanguageCode = e.LanguageCode,
                CategoryName = e.CategoryName,
                AuthorName = e.AuthorName
            })
            .ToList();
    }

    public async Task RebuildAllAsync(CancellationToken ct = default)
    {
        var ids = await _db.Posts.AsNoTracking()
            .Where(p => !p.IsDeleted && p.IsPublished)
            .Select(p => p.Id)
            .ToListAsync(ct);

        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            await IndexPostAsync(id, ct);
        }

        _logger.LogInformation("Search index rebuild complete posts={Count}", ids.Count);
    }

    private async Task UpsertFtsAsync(
        int postId, string title, string? summary, string body, string tags,
        string? category, string? author, string lang, CancellationToken ct)
    {
        try
        {
            await _db.Database.ExecuteSqlRawAsync("DELETE FROM PostsFts WHERE PostId = {0}", postId);
            await _db.Database.ExecuteSqlRawAsync(
                "INSERT INTO PostsFts(Title, Summary, BodyText, TagsCsv, CategoryName, AuthorName, LanguageCode, PostId) VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7})",
                title ?? "", summary ?? "", body ?? "", tags ?? "", category ?? "", author ?? "", lang ?? "fa", postId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PostsFts upsert failed PostId={Id}", postId);
        }
    }

    private async Task<List<int>> FallbackLikeIdsAsync(string query, int take, CancellationToken ct)
    {
        var term = query.Trim();
        if (term.Length > 80) term = term[..80];

        return await _db.SearchIndexEntries.AsNoTracking()
            .Where(s => s.IsPublished)
            .Where(s =>
                s.Title.Contains(term)
                || (s.Summary != null && s.Summary.Contains(term))
                || (s.BodyText != null && s.BodyText.Contains(term))
                || (s.TagsCsv != null && s.TagsCsv.Contains(term))
                || (s.CategoryName != null && s.CategoryName.Contains(term))
                || (s.AuthorName != null && s.AuthorName.Contains(term)))
            .OrderByDescending(s => s.PublishedAtUtc)
            .Take(take)
            .Select(s => s.PostId)
            .ToListAsync(ct);
    }

    public static string? BuildMatchQuery(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var cleaned = FtsSpecial.Replace(raw.Trim(), " ");
        var parts = cleaned.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return null;

        var tokens = new List<string>();
        foreach (var p in parts.Take(12))
        {
            var t = p;
            if (t.Length > 64) t = t[..64];
            if (t.Length < 1) continue;
            tokens.Add("\"" + t + "\"*");
        }
        if (tokens.Count == 0) return null;
        return string.Join(' ', tokens);
    }

    private static string StripMarkdown(string? md)
    {
        if (string.IsNullOrWhiteSpace(md)) return string.Empty;
        var s = MdNoise.Replace(md, " ");
        s = Regex.Replace(s, @"\s+", " ");
        return s.Trim();
    }
}

public sealed class SearchHit
{
    public int PostId { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Summary { get; set; }
    public string LanguageCode { get; set; } = "fa";
    public string? CategoryName { get; set; }
    public string? AuthorName { get; set; }
}
