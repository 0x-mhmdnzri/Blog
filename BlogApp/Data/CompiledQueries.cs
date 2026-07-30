using BlogApp.Models;
using BlogApp.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

/// <summary>
/// EF Core compiled queries — parse/compile once, reuse on hot paths (home feed).
/// </summary>
public static class CompiledQueries
{
    /// <summary>
    /// Categories are a small table; compiled as sync enumerable then buffered.
    /// (CompileAsyncQuery + OrderBy inferred Task&lt;IOrderedQueryable&gt; under EF 10.)
    /// </summary>
    private static readonly Func<ApplicationDbContext, IEnumerable<Category>> CategoriesOrderedCore =
        EF.CompileQuery((ApplicationDbContext db) =>
            db.Categories.AsNoTracking().OrderBy(c => c.Name));

    public static Task<List<Category>> CategoriesOrderedAsync(ApplicationDbContext db) =>
        Task.FromResult(CategoriesOrderedCore(db).ToList());

    /// <summary>
    /// Lean home-feed page: list columns only (never ContentMarkdown / large blobs).
    /// </summary>
    public static readonly Func<
        ApplicationDbContext,
        string,
        DateTime,
        int,
        int,
        IAsyncEnumerable<PostListItemViewModel>> HomeRecentPage =
        EF.CompileAsyncQuery((ApplicationDbContext db, string lang, DateTime now, int skip, int take) =>
            db.Posts.AsNoTracking()
                .Where(p => !p.IsDeleted)
                .Where(p => p.LanguageCode == lang)
                .Where(p => p.IsPublished
                            || (p.ScheduledPublishAtUtc != null && p.ScheduledPublishAtUtc <= now))
                .Where(p => p.ExpiresAtUtc == null || p.ExpiresAtUtc > now)
                .Where(p => p.TranslationStatus == TranslationStatus.Original
                            || p.TranslationStatus == TranslationStatus.Approved)
                .OrderByDescending(p => p.IsSticky)
                .ThenByDescending(p => p.IsFeatured)
                .ThenByDescending(p => p.IsPublished ? p.PublishedAtUtc : p.CreatedAtUtc)
                .Skip(skip)
                .Take(take)
                .Select(p => new PostListItemViewModel
                {
                    Id = p.Id,
                    Title = p.Title,
                    Slug = p.Slug,
                    Summary = p.Summary,
                    CategoryName = p.Category != null ? p.Category.Name : null,
                    PublishedAtUtc = p.PublishedAtUtc,
                    CoverMediaAssetId = p.CoverMediaAssetId,
                    IsPublished = p.IsPublished,
                    IsFeatured = p.IsFeatured,
                    IsSticky = p.IsSticky,
                    ReadingTimeMinutes = p.ReadingTimeMinutes,
                    LanguageCode = p.LanguageCode,
                    Tags = p.PostTags.Select(pt => pt.Tag.Name).ToList()
                }));

    public static readonly Func<
        ApplicationDbContext,
        string,
        DateTime,
        Task<int>> HomeRecentCount =
        EF.CompileAsyncQuery((ApplicationDbContext db, string lang, DateTime now) =>
            db.Posts.AsNoTracking()
                .Where(p => !p.IsDeleted)
                .Where(p => p.LanguageCode == lang)
                .Where(p => p.IsPublished
                            || (p.ScheduledPublishAtUtc != null && p.ScheduledPublishAtUtc <= now))
                .Where(p => p.ExpiresAtUtc == null || p.ExpiresAtUtc > now)
                .Where(p => p.TranslationStatus == TranslationStatus.Original
                            || p.TranslationStatus == TranslationStatus.Approved)
                .Count());
}
