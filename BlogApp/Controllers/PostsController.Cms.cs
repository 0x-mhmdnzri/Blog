using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public partial class PostsController
{
    private async Task SaveRevisionAsync(Post post, string userId, string note)
    {
        var revision = new PostRevision
        {
            PostId = post.Id,
            Title = post.Title,
            Summary = post.Summary,
            ContentMarkdown = post.ContentMarkdown,
            CreatedAtUtc = DateTime.UtcNow,
            Note = note,
            CreatedByUserId = userId
        };
        _db.PostRevisions.Add(revision);
        var old = await _db.PostRevisions
            .Where(r => r.PostId == post.Id)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip(49)
            .ToListAsync();
        if (old.Count > 0)
            _db.PostRevisions.RemoveRange(old);
        await _db.SaveChangesAsync();
    }

    private async Task ApplyScheduledAndExpirationAsync()
    {
        var now = DateTime.UtcNow;
        var toPublish = await _db.Posts
            .Where(p => !p.IsDeleted && !p.IsPublished
                        && p.ScheduledPublishAtUtc != null
                        && p.ScheduledPublishAtUtc <= now)
            .ToListAsync();
        foreach (var p in toPublish)
        {
            p.IsPublished = true;
            p.PublishedAtUtc ??= now;
            p.ScheduledPublishAtUtc = null;
            p.UpdatedAtUtc = now;
        }
        var toExpire = await _db.Posts
            .Where(p => !p.IsDeleted && p.IsPublished
                        && p.ExpiresAtUtc != null
                        && p.ExpiresAtUtc <= now)
            .ToListAsync();
        foreach (var p in toExpire)
        {
            p.IsPublished = false;
            p.UpdatedAtUtc = now;
        }
        if (toPublish.Count + toExpire.Count > 0)
            await _db.SaveChangesAsync();
    }

    private async Task<List<CategoryOption>> GetCategoryOptionsAsync() =>
        await _db.Categories.OrderBy(c => c.Name)
            .Select(c => new CategoryOption { Id = c.Id, Name = c.Name })
            .ToListAsync();

    private async Task<string> MakeUniqueSlugAsync(string baseSlug, string? languageCode = null)
    {
        var lang = AppCultures.Normalize(languageCode ?? _culture.CurrentCode);
        var slug = baseSlug;
        var i = 2;
        while (await _db.Posts.AnyAsync(p => p.Slug == slug && p.LanguageCode == lang && !p.IsDeleted))
            slug = $"{baseSlug}-{i++}";
        return slug;
    }

    private async Task ApplyTagsAsync(Post post, string? tagsCsv)
    {
        if (string.IsNullOrWhiteSpace(tagsCsv)) return;
        var names = tagsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            var slug = BlogApp.Services.SlugHelper.Slugify(name);
            var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Slug == slug);
            if (tag is null)
            {
                tag = new Tag { Name = name, Slug = slug };
                _db.Tags.Add(tag);
            }
            post.PostTags.Add(new PostTag { Tag = tag, Post = post });
        }
    }
}
