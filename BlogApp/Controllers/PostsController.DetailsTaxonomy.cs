using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

/// <summary>Related posts + series context loaded for Details view.</summary>
public partial class PostsController
{
    private async Task LoadTaxonomyContextAsync(Post post)
    {
        // Related by shared tags (excluding self)
        var tagIds = post.PostTags.Select(pt => pt.TagId).ToList();
        List<RelatedPostItem> related;
        if (tagIds.Count == 0)
        {
            related = new List<RelatedPostItem>();
        }
        else
        {
            related = await _db.Posts
                .Where(p => !p.IsDeleted && p.IsPublished && p.Id != post.Id)
                .Where(p => p.PostTags.Any(pt => tagIds.Contains(pt.TagId)))
                .Select(p => new RelatedPostItem
                {
                    Title = p.Title,
                    Slug = p.Slug,
                    Summary = p.Summary,
                    SharedTagCount = p.PostTags.Count(pt => tagIds.Contains(pt.TagId))
                })
                .OrderByDescending(p => p.SharedTagCount)
                .ThenByDescending(p => p.Title)
                .Take(6)
                .ToListAsync();
        }
        ViewBag.RelatedPosts = related;

        // Series memberships
        var series = await _db.SeriesPosts
            .Where(sp => sp.PostId == post.Id)
            .Include(sp => sp.Series).ThenInclude(s => s.Posts).ThenInclude(m => m.Post)
            .Select(sp => sp.Series)
            .ToListAsync();

        ViewBag.SeriesList = series.Select(s => new
        {
            s.Name,
            s.Slug,
            Posts = s.Posts
                .Where(m => m.Post is { IsDeleted: false, IsPublished: true } || m.PostId == post.Id)
                .OrderBy(m => m.SortOrder)
                .Select(m => new { m.Post.Title, m.Post.Slug, m.SortOrder, IsCurrent = m.PostId == post.Id })
                .ToList()
        }).ToList();
    }
}
