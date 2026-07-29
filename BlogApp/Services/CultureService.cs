using BlogApp.Data;
using BlogApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Services;

public interface ICultureService
{
    string CurrentCode { get; }
    CultureDescriptor Current { get; }
    IReadOnlyList<CultureDescriptor> EnabledCultures { get; }
    string DefaultCode { get; }
    Task<List<PostTranslationLink>> GetTranslationLinksAsync(int postId, CancellationToken ct = default);
    Task<Post?> GetSiblingAsync(int translationGroupId, string languageCode, CancellationToken ct = default);
    Task<Post> CreateTranslationDraftAsync(Post source, string targetLanguage, string authorUserId, CancellationToken ct = default);
}

/// <summary>
/// Resolves current culture from HttpContext (set by CultureMiddleware)
/// and manages multi-language post groups.
/// </summary>
public sealed class CultureService : ICultureService
{
    public const string HttpContextKey = "AppCulture";
    public const string CookieName = "Blog.Culture";

    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly ISiteConfigService _config;

    public CultureService(
        ApplicationDbContext db,
        IHttpContextAccessor http,
        ISiteConfigService config)
    {
        _db = db;
        _http = http;
        _config = config;
    }

    public string DefaultCode => AppCultures.Default;

    public CultureDescriptor Current
    {
        get
        {
            if (_http.HttpContext?.Items.TryGetValue(HttpContextKey, out var obj) == true
                && obj is CultureDescriptor d)
                return d;
            return AppCultures.Find(AppCultures.Default)!;
        }
    }

    public string CurrentCode => Current.Code;

    public IReadOnlyList<CultureDescriptor> EnabledCultures
    {
        get
        {
            // Enabled list can later be filtered via site setting; for now all catalog entries.
            return AppCultures.All;
        }
    }

    public async Task<List<PostTranslationLink>> GetTranslationLinksAsync(int postId, CancellationToken ct = default)
    {
        var post = await _db.Posts.AsNoTracking()
            .Where(p => p.Id == postId && !p.IsDeleted)
            .Select(p => new { p.Id, p.TranslationGroupId })
            .FirstOrDefaultAsync(ct);
        if (post is null) return new();

        var groupId = post.TranslationGroupId ?? post.Id;
        var rows = await _db.Posts.AsNoTracking()
            .Where(p => !p.IsDeleted && (p.TranslationGroupId == groupId || p.Id == groupId))
            .Select(p => new { p.Id, p.LanguageCode, p.Slug, p.Title, p.IsPublished, p.TranslationStatus })
            .ToListAsync(ct);

        return rows.Select(r =>
        {
            var c = AppCultures.Find(r.LanguageCode) ?? AppCultures.Find(AppCultures.Default)!;
            return new PostTranslationLink
            {
                PostId = r.Id,
                LanguageCode = c.Code,
                Slug = r.Slug,
                Title = r.Title,
                IsPublished = r.IsPublished,
                Status = r.TranslationStatus,
                NativeName = c.NativeName,
                IsRtl = c.IsRtl
            };
        }).OrderBy(x => x.LanguageCode).ToList();
    }

    public Task<Post?> GetSiblingAsync(int translationGroupId, string languageCode, CancellationToken ct = default)
    {
        var code = AppCultures.Normalize(languageCode);
        return _db.Posts.FirstOrDefaultAsync(
            p => !p.IsDeleted
                 && p.LanguageCode == code
                 && (p.TranslationGroupId == translationGroupId || p.Id == translationGroupId),
            ct);
    }

    public async Task<Post> CreateTranslationDraftAsync(
        Post source,
        string targetLanguage,
        string authorUserId,
        CancellationToken ct = default)
    {
        var code = AppCultures.Normalize(targetLanguage);
        if (code == AppCultures.Normalize(source.LanguageCode))
            throw new InvalidOperationException("Cannot create translation in the same language.");

        var groupId = source.TranslationGroupId ?? source.Id;

        // Ensure source has a group id
        if (source.TranslationGroupId is null)
        {
            source.TranslationGroupId = groupId;
            source.TranslationStatus = TranslationStatus.Original;
            source.LanguageCode = AppCultures.Normalize(source.LanguageCode);
        }

        var existing = await GetSiblingAsync(groupId, code, ct);
        if (existing is not null)
            return existing;

        var slugBase = source.Slug;
        var uniqueSlug = await MakeUniqueSlugAsync(slugBase, code, ct);

        var draft = new Post
        {
            Title = source.Title + $" [{code}]",
            Slug = uniqueSlug,
            Summary = source.Summary,
            ContentMarkdown = source.ContentMarkdown,
            CoverMediaAssetId = source.CoverMediaAssetId,
            AuthorId = authorUserId,
            CategoryId = source.CategoryId,
            IsPublished = false,
            LanguageCode = code,
            TranslationGroupId = groupId,
            TranslationStatus = TranslationStatus.Draft,
            ReadingTimeMinutes = source.ReadingTimeMinutes,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.Posts.Add(draft);
        await _db.SaveChangesAsync(ct);
        return draft;
    }

    private async Task<string> MakeUniqueSlugAsync(string baseSlug, string languageCode, CancellationToken ct)
    {
        var slug = baseSlug;
        var n = 0;
        while (await _db.Posts.AnyAsync(p => p.Slug == slug && p.LanguageCode == languageCode && !p.IsDeleted, ct))
        {
            n++;
            slug = $"{baseSlug}-{n}";
        }
        return slug;
    }
}
