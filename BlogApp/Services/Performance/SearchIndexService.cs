using System.Text;
using System.Text.RegularExpressions;
using BlogApp.Data;
using BlogApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Services.Performance;

public sealed class SearchIndexService
{
    private static readonly Regex MdNoise = new(@"[#*_`>~\[\]()!]|\{+[^}]*\}+", RegexOptions.Compiled);

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

        if (post is null)
        {
            await RemovePostAsync(postId, ct);
            return;
        }

        if (post.IsDeleted || !post.IsPublished)
        {
            await RemovePostAsync(postId, ct);
            return;
        }

        var existing = await _db.SearchIndexEntries.AsTracking()
            .FirstOrDefaultAsync(s => s.PostId == postId, ct);

        var body = StripMarkdown(post.ContentMarkdown);
        if (body.Length > 50_000) body = body[..50_000];

        var tags = string.Join(',', post.PostTags.Select(t => t.Tag.Name));

        if (existing is null)
        {
            existing = new SearchIndexEntry { PostId = postId };
            _db.SearchIndexEntries.Add(existing);
        }

        existing.LanguageCode = post.LanguageCode;
        existing.Title = post.Title;
        existing.Slug = post.Slug;
        existing.Summary = post.Summary;
        existing.BodyText = body;
        existing.TagsCsv = tags;
        existing.CategoryName = post.Category?.Name;
        existing.IsPublished = post.IsPublished;
        existing.PublishedAtUtc = post.PublishedAtUtc;
        existing.IndexedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        _logger.LogDebug("Search index updated PostId={Id}", postId);
    }

    public async Task RemovePostAsync(int postId, CancellationToken ct = default)
    {
        var rows = await _db.SearchIndexEntries.Where(s => s.PostId == postId).ToListAsync(ct);
        if (rows.Count == 0) return;
        _db.SearchIndexEntries.RemoveRange(rows);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<SearchIndexEntry>> SearchAsync(string query, string lang, int take = 20, CancellationToken ct = default)
    {
        var term = query.Trim();
        if (term.Length < 2) return new List<SearchIndexEntry>();
        if (term.Length > 80) term = term[..80];

        return await _db.SearchIndexEntries.AsNoTracking()
            .Where(s => s.IsPublished && s.LanguageCode == lang)
            .Where(s =>
                s.Title.Contains(term)
                || (s.Summary != null && s.Summary.Contains(term))
                || (s.BodyText != null && s.BodyText.Contains(term))
                || (s.TagsCsv != null && s.TagsCsv.Contains(term))
                || (s.CategoryName != null && s.CategoryName.Contains(term)))
            .OrderByDescending(s => s.PublishedAtUtc)
            .Take(take)
            .ToListAsync(ct);
    }

    private static string StripMarkdown(string? md)
    {
        if (string.IsNullOrWhiteSpace(md)) return string.Empty;
        var s = MdNoise.Replace(md, " ");
        s = Regex.Replace(s, "\\s+", " ");
        return s.Trim();
    }
}
