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
        var lang = source.LanguageCode;
        var copy = new Post
        {
            Title = source.Title + " (copy)",
            Slug = await MakeUniqueSlugAsync(SlugHelper.Slugify(source.Title + "-copy"), lang),
            Summary = source.Summary,
            ContentMarkdown = source.ContentMarkdown,
            CoverMediaAssetId = source.CoverMediaAssetId,
            AuthorId = authorId,
            CategoryId = source.CategoryId,
            IsPublished = false,
            IsFeatured = false,
            IsSticky = false,
            IsPremium = source.IsPremium,
            IsSponsored = source.IsSponsored,
            SponsoredLabel = source.SponsoredLabel,
            ReadingTimeMinutes = source.ReadingTimeMinutes,
            LanguageCode = lang,
            TranslationStatus = TranslationStatus.Original,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        foreach (var pt in source.PostTags)
            copy.PostTags.Add(new PostTag { TagId = pt.TagId });
        _db.Posts.Add(copy);
        await _db.SaveChangesAsync();
        copy.TranslationGroupId = copy.Id;
        await _db.SaveChangesAsync();
        await SaveRevisionAsync(copy, authorId, "dup-" + source.Id);
        return RedirectToAction(nameof(Edit), new { id = copy.Id });
    }

    /// <summary>
    /// Continuous server autosave. When id=0, creates an unpublished draft and returns new id
    /// so the editor can keep saving without a full form post.
    /// </summary>
    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AutoSave(
        [FromForm] int id,
        [FromForm] string title,
        [FromForm] string contentMarkdown,
        [FromForm] string? summary,
        [FromForm] string? languageCode)
    {
        var authorId = AuthorAccess.UserId(User)!;
        title = (title ?? string.Empty).Trim();
        contentMarkdown ??= string.Empty;

        if (id <= 0)
        {
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(contentMarkdown))
                return Json(new { ok = false, error = "empty" });

            var lang = AppCultures.Normalize(languageCode ?? _culture.CurrentCode);
            var draftTitle = string.IsNullOrWhiteSpace(title) ? "Untitled draft" : title;
            var post = new Post
            {
                Title = draftTitle,
                Slug = await MakeUniqueSlugAsync(SlugHelper.Slugify(draftTitle), lang),
                Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim(),
                ContentMarkdown = contentMarkdown,
                AuthorId = authorId,
                IsPublished = false,
                LanguageCode = lang,
                TranslationStatus = TranslationStatus.Original,
                ReadingTimeMinutes = _markdown.EstimateReadingTimeMinutes(contentMarkdown),
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _db.Posts.Add(post);
            await _db.SaveChangesAsync();
            post.TranslationGroupId = post.Id;
            await _db.SaveChangesAsync();
            await SaveRevisionAsync(post, authorId, "autosave-create");
            return Json(new
            {
                ok = true,
                id = post.Id,
                updatedAtUtc = post.UpdatedAtUtc,
                readingTimeMinutes = post.ReadingTimeMinutes,
                created = true
            });
        }

        var existing = await _db.Posts.FindAsync(id);
        if (existing is null) return NotFound();
        if (!AuthorAccess.OwnsPost(User, existing)) return Forbid();
        if (existing.IsDeleted) return BadRequest("deleted");

        var changed = existing.ContentMarkdown != contentMarkdown
                      || existing.Title != (string.IsNullOrWhiteSpace(title) ? existing.Title : title);
        if (changed)
            await SaveRevisionAsync(existing, authorId, "autosave");

        if (!string.IsNullOrWhiteSpace(title))
            existing.Title = title;
        existing.ContentMarkdown = contentMarkdown;
        if (!string.IsNullOrWhiteSpace(summary))
            existing.Summary = summary.Trim();
        existing.ReadingTimeMinutes = _markdown.EstimateReadingTimeMinutes(existing.ContentMarkdown);
        existing.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Json(new
        {
            ok = true,
            id = existing.Id,
            updatedAtUtc = existing.UpdatedAtUtc,
            readingTimeMinutes = existing.ReadingTimeMinutes,
            created = false
        });
    }

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpGet]
    public async Task<IActionResult> Revision(int postId, int revisionId)
    {
        var post = await _db.Posts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted);
        if (post is null) return NotFound();
        if (!AuthorAccess.OwnsPost(User, post)) return Forbid();

        var revision = await _db.PostRevisions.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == revisionId && r.PostId == postId);
        if (revision is null) return NotFound();

        ViewData["Title"] = "Revision " + revision.CreatedAtUtc.ToString("u");
        ViewData["UseAdminLayout"] = true;
        ViewData["NoIndex"] = true;
        ViewBag.Post = post;
        ViewBag.CurrentMarkdown = post.ContentMarkdown;
        return View(revision);
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
        post.Title = revision.Title;
        post.Summary = revision.Summary;
        post.ContentMarkdown = revision.ContentMarkdown;
        post.ReadingTimeMinutes = _markdown.EstimateReadingTimeMinutes(post.ContentMarkdown);
        post.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Edit), new { id = postId });
    }

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AiSummarize([FromForm] string content)
    {
        var summary = await _ai.SummarizeAsync(content ?? "");
        return Json(new { summary });
    }

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AiGrammarCheck([FromForm] string content)
    {
        var hints = await _ai.CheckGrammarAndStyleAsync(content ?? "");
        return Json(new { hints });
    }

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AiAssist([FromForm] string content)
    {
        var (title, tags) = await _ai.AssistContentGenerationAsync(content ?? "");
        return Json(new { suggestedTitle = title, suggestedTags = tags });
    }
}
