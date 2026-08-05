using BlogApp.Data;
using BlogApp.Developer.Domain;
using BlogApp.Developer.Messaging;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using BlogApp.Services.Analytics;
using BlogApp.Services.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public partial class PostsController
{
    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var post = await _db.Posts.Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (post is null) return NotFound();
        if (!AuthorAccess.OwnsPost(User, post)) return Forbid();

        var vm = new PostEditViewModel
        {
            Id = post.Id,
            Title = post.Title,
            Summary = post.Summary,
            ContentMarkdown = post.ContentMarkdown,
            CategoryId = post.CategoryId,
            CoverMediaAssetId = post.CoverMediaAssetId,
            IsPublished = post.IsPublished,
            ScheduledPublishAtUtc = post.ScheduledPublishAtUtc,
            ExpiresAtUtc = post.ExpiresAtUtc,
            IsFeatured = post.IsFeatured,
            IsSticky = post.IsSticky,
            IsPremium = post.IsPremium,
            IsSponsored = post.IsSponsored,
            SponsoredLabel = post.SponsoredLabel,
            TagsCsv = string.Join(", ", post.PostTags.Select(pt => pt.Tag.Name)),
            LanguageCode = post.LanguageCode,
            TranslationStatus = post.TranslationStatus,
            AvailableCategories = await GetCategoryOptionsAsync()
        };
        return View(vm);
    }

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PostEditViewModel vm)
    {
        if (id <= 0 && vm.Id > 0) id = vm.Id;
        if (vm.Id <= 0 && id > 0) vm.Id = id;
        if (id != vm.Id) return BadRequest();

        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;

        var post = await _db.Posts.Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (post is null) return NotFound();
        if (!AuthorAccess.OwnsPost(User, post)) return Forbid();

        if (string.IsNullOrWhiteSpace(vm.ContentMarkdown)
            && Request.Form.TryGetValue("ContentMarkdown", out var rawBody)
            && !string.IsNullOrWhiteSpace(rawBody))
        {
            vm.ContentMarkdown = rawBody.ToString();
            ModelState.Remove(nameof(vm.ContentMarkdown));
        }

        if (string.IsNullOrWhiteSpace(vm.ContentMarkdown))
        {
            if (!string.IsNullOrWhiteSpace(post.ContentMarkdown))
            {
                vm.ContentMarkdown = post.ContentMarkdown;
                ModelState.Remove(nameof(vm.ContentMarkdown));
            }
            else
            {
                ModelState.AddModelError(nameof(vm.ContentMarkdown), "محتوای نوشته الزامی است");
            }
        }

        if (!ModelState.IsValid)
        {
            vm.AvailableCategories = await GetCategoryOptionsAsync();
            return View(vm);
        }

        var authorId = AuthorAccess.UserId(User)!;
        var wasPublished = post.IsPublished;
        var wantPublish = ResolvePublishFlag(vm);
        var changed = post.Title != vm.Title || post.ContentMarkdown != vm.ContentMarkdown || post.Summary != vm.Summary;

        post.Title = vm.Title;
        post.Summary = string.IsNullOrWhiteSpace(vm.Summary) ? _ai.Summarize(vm.ContentMarkdown) : vm.Summary;
        post.ContentMarkdown = vm.ContentMarkdown;
        post.CategoryId = vm.CategoryId;
        post.CoverMediaAssetId = vm.CoverMediaAssetId;
        post.IsFeatured = vm.IsFeatured;
        post.IsSticky = vm.IsSticky;
        post.IsPremium = vm.IsPremium;
        post.IsSponsored = vm.IsSponsored;
        post.SponsoredLabel = vm.SponsoredLabel?.Trim();
        post.ReadingTimeMinutes = _markdown.EstimateReadingTimeMinutes(vm.ContentMarkdown);
        post.UpdatedAtUtc = DateTime.UtcNow;
        post.TranslationStatus = vm.TranslationStatus;

        ApplyPublishState(post, wantPublish, vm.ScheduledPublishAtUtc, vm.ExpiresAtUtc, wasPublished);

        _db.PostTags.RemoveRange(post.PostTags);
        await ApplyTagsAsync(post, vm.TagsCsv);
        await _db.SaveChangesAsync();
        if (changed) await SaveRevisionAsync(post, authorId, "after-edit");

        if (post.ReviewStatus == PostReviewStatus.PendingReview && !wasPublished)
            await NotifySuperAdminsPendingPostAsync(post);

        try
        {
            if (!wasPublished && post.IsPublished && post.PublishedAtUtc.HasValue)
                await _events.PublishAsync(new PostPublishedDomainEvent(post.Id, post.Title, post.Slug, post.AuthorId, post.PublishedAtUtc.Value));
            else if (wasPublished && !post.IsPublished && post.ScheduledPublishAtUtc is null)
                await _events.PublishAsync(new PostUnpublishedDomainEvent(post.Id, post.Slug));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Domain event publish failed after Edit PostId={Id}", post.Id);
        }

        return Redirect($"/{post.LanguageCode}/post/{post.Slug}");
    }

    private bool ResolvePublishFlag(PostEditViewModel vm)
    {
        if (vm.IsPublished) return true;
        if (Request.Form.TryGetValue("IsPublished", out var vals))
        {
            foreach (var v in vals)
            {
                if (string.Equals(v, "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(v, "on", StringComparison.OrdinalIgnoreCase)
                    || v == "1")
                    return true;
            }
        }
        return false;
    }

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTranslation(int id, string targetLanguage)
    {
        var source = await _db.Posts.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (source is null) return NotFound();
        if (!AuthorAccess.OwnsPost(User, source)) return Forbid();

        if (!AppCultures.IsSupported(targetLanguage))
        {
            TempData["Error"] = "Language not supported.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var draft = await _culture.CreateTranslationDraftAsync(source, targetLanguage, AuthorAccess.UserId(User)!);
        return RedirectToAction(nameof(Edit), new { id = draft.Id });
    }

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult PreviewMarkdown([FromForm] string content) =>
        Content(_markdown.RenderToHtmlWithToc(content ?? "", false), "text/html");
}
