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
    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> HardDelete(int id)
    {
        var post = await _db.Posts.FindAsync(id);
        if (post is null) return NotFound();
        if (!AuthorAccess.OwnsPost(User, post)) return Forbid();
        _db.Posts.Remove(post);
        await _db.SaveChangesAsync();
        return RedirectToAction("Posts", "Admin");
    }

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Duplicate(int id)
    {
        var source = await _db.Posts.Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (source is null) return NotFound();
        if (!AuthorAccess.OwnsPost(User, source)) return Forbid();
        var authorId = AuthorAccess.UserId(User)!;
        var copy = new Post
        {
            Title = source.Title + " (copy)",
            Slug = await MakeUniqueSlugAsync(SlugHelper.Slugify(source.Title + "-copy")),
            Summary = source.Summary, ContentMarkdown = source.ContentMarkdown,
            CoverMediaAssetId = source.CoverMediaAssetId, AuthorId = authorId, CategoryId = source.CategoryId,
            IsPublished = false, ReadingTimeMinutes = source.ReadingTimeMinutes,
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        foreach (var pt in source.PostTags) copy.PostTags.Add(new PostTag { TagId = pt.TagId });
        _db.Posts.Add(copy);
        await _db.SaveChangesAsync();
        await SaveRevisionAsync(copy, authorId, "dup-" + source.Id);
        return RedirectToAction(nameof(Edit), new { id = copy.Id });
    }

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AutoSave([FromForm] int id, [FromForm] string title, [FromForm] string contentMarkdown, [FromForm] string? summary)
    {
        var post = await _db.Posts.FindAsync(id);
        if (post is null) return NotFound();
        if (!AuthorAccess.OwnsPost(User, post)) return Forbid();
        if (post.IsDeleted) return BadRequest("deleted");
        var authorId = AuthorAccess.UserId(User)!;
        var changed = post.ContentMarkdown != (contentMarkdown ?? "") || post.Title != (title ?? post.Title);
        if (changed) await SaveRevisionAsync(post, authorId, "autosave");
        post.Title = string.IsNullOrWhiteSpace(title) ? post.Title : title.Trim();
        post.ContentMarkdown = contentMarkdown ?? post.ContentMarkdown;
        if (!string.IsNullOrWhiteSpace(summary)) post.Summary = summary.Trim();
        post.ReadingTimeMinutes = _markdown.EstimateReadingTimeMinutes(post.ContentMarkdown);
        post.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Json(new { ok = true, updatedAtUtc = post.UpdatedAtUtc, readingTimeMinutes = post.ReadingTimeMinutes });
    }

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreRevision(int postId, int revisionId)
    {
        var post = await _db.Posts.FindAsync(postId);
        if (post is null || post.IsDeleted) return NotFound();
        if (!AuthorAccess.OwnsPost(User, post)) return Forbid();
        var revision = await _db.PostRevisions.FirstOrDefaultAsync(r => r.Id == revisionId && r.PostId == postId);
        if (revision is null) return NotFound();
        var authorId = AuthorAccess.UserId(User)!;
        await SaveRevisionAsync(post, authorId, "before-restore-" + revisionId);
        post.Title = revision.Title; post.Summary = revision.Summary; post.ContentMarkdown = revision.ContentMarkdown;
        post.ReadingTimeMinutes = _markdown.EstimateReadingTimeMinutes(post.ContentMarkdown);
        post.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Edit), new { id = postId });
    }

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult AiSummarize([FromForm] string content) =>
        Json(new { summary = _ai.Summarize(content ?? "") });

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult AiGrammarCheck([FromForm] string content) =>
        Json(new { hints = _ai.CheckGrammarAndStyle(content ?? "") });

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult AiAssist([FromForm] string content)
    {
        var (title, tags) = _ai.AssistContentGeneration(content ?? "");
        return Json(new { suggestedTitle = title, suggestedTags = tags });
    }
}
