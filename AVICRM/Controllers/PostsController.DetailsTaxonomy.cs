using AVICRM.Models;
using AVICRM.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AVICRM.Controllers;

public partial class PostsController
{
    /// <summary>Related posts by shared tags (same language) + category fallback + series navigation.</summary>
    private async Task LoadTaxonomyContextAsync(Post post)
    {
        var tagIds = post.PostTags.Select(pt => pt.TagId).ToList();
        List<RelatedPostItem> related;

        if (tagIds.Count > 0)
        {
            var candidates = await _db.Posts
                .Where(p => !p.IsDeleted && p.IsPublished && p.Id != post.Id && p.LanguageCode == post.LanguageCode)
                .Where(p => p.PostTags.Any(pt => tagIds.Contains(pt.TagId)))
                .Select(p => new
                {
                    p.Title,
                    p.Slug,
                    p.Summary,
                    p.LanguageCode,
                    Shared = p.PostTags.Count(pt => tagIds.Contains(pt.TagId)),
                    p.PublishedAtUtc
                })
                .OrderByDescending(p => p.Shared)
                .ThenByDescending(p => p.PublishedAtUtc)
                .Take(8)
                .ToListAsync();

            related = candidates.Select(p => new RelatedPostItem
            {
                Title = p.Title,
                Slug = p.Slug,
                Summary = p.Summary,
                SharedTagCount = p.Shared
            }).ToList();
        }
        else
        {
            related = new List<RelatedPostItem>();
        }

        // Category fallback when tags yield few results
        if (related.Count < 4 && post.CategoryId is int catId)
        {
            var have = related.Select(r => r.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var more = await _db.Posts
                .Where(p => !p.IsDeleted && p.IsPublished && p.Id != post.Id
                            && p.LanguageCode == post.LanguageCode
                            && p.CategoryId == catId)
                .OrderByDescending(p => p.PublishedAtUtc)
                .Take(8)
                .Select(p => new { p.Title, p.Slug, p.Summary })
                .ToListAsync();

            foreach (var p in more)
            {
                if (have.Contains(p.Slug)) continue;
                related.Add(new RelatedPostItem
                {
                    Title = p.Title,
                    Slug = p.Slug,
                    Summary = p.Summary,
                    SharedTagCount = 0
                });
                if (related.Count >= 8) break;
            }
        }

        ViewBag.RelatedPosts = related;

        var memberships = await _db.SeriesPosts
            .Where(sp => sp.PostId == post.Id)
            .Select(sp => sp.SeriesId)
            .ToListAsync();

        if (memberships.Count == 0)
        {
            ViewBag.SeriesList = new List<object>();
            return;
        }

        var series = await _db.PostSeries
            .Where(s => memberships.Contains(s.Id))
            .Include(s => s.Posts).ThenInclude(m => m.Post)
            .ToListAsync();

        ViewBag.SeriesList = series.Select(s => new
        {
            s.Name,
            s.Slug,
            Posts = s.Posts
                .Where(m => m.Post != null && (!m.Post.IsDeleted && m.Post.IsPublished || m.PostId == post.Id))
                .OrderBy(m => m.SortOrder)
                .Select(m => new
                {
                    m.Post.Title,
                    m.Post.Slug,
                    m.SortOrder,
                    IsCurrent = m.PostId == post.Id
                })
                .ToList()
        }).ToList();
    }
}
