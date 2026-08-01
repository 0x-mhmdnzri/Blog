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
    [HttpGet("post/{slug}")]
    public async Task<IActionResult> Details(string slug, string? sort = null)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 220)
            return NotFound();

        await ApplyScheduledAndExpirationAsync();
        var lang = _culture.CurrentCode;

        var post = await _db.Posts.Include(p => p.Category).Include(p => p.Author)
            .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
            .Include(p => p.Comments.Where(c => c.Status == CommentStatus.Approved))
            .FirstOrDefaultAsync(p => p.Slug == slug && p.LanguageCode == lang && !p.IsDeleted);

        if (post is null)
        {
            var any = await _db.Posts.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Slug == slug && !p.IsDeleted);
            if (any is not null)
                return Redirect($"/{any.LanguageCode}/post/{any.Slug}");
            return NotFound();
        }

        if (!post.IsPublished && !User.Identity!.IsAuthenticated)
            return NotFound();
        if (!post.IsPublished && !AuthorAccess.OwnsPost(User, post))
            return NotFound();

        post.ViewCount++;
        await _db.SaveChangesAsync();

        ViewBag.Html = _markdown.RenderToHtmlWithToc(post.ContentMarkdown, true);
        ViewBag.CommentSort = string.Equals(sort, "latest", StringComparison.OrdinalIgnoreCase) ? "latest" : "relevant";
        ViewData["Title"] = post.Title;

        try { await LoadSocialContextAsync(post); } catch { }

        return View(post);
    }

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var vm = new PostEditViewModel
        {
            AvailableCategories = await GetCategoryOptionsAsync(),
            LanguageCode = _culture.CurrentCode,
            TranslationStatus = TranslationStatus.Original
        };
        return View(vm);
    }

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PostEditViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            vm.AvailableCategories = await GetCategoryOptionsAsync();
            return View(vm);
        }
        var authorId = AuthorAccess.UserId(User)!;
        var lang = AppCultures.Normalize(vm.LanguageCode);
        var uniqueSlug = await MakeUniqueSlugAsync(SlugHelper.Slugify(vm.Title), lang);
        var post = new Post
        {
            Title = vm.Title,
            AuthorId = authorId,
            Soft = uniqueSlug,
            Summary = string.IsNullOrWhiteSpace(vm.Summary) ? _ai.Summarize(vm.ContentMarkdown) : vm.Summary,
            ContentMarkdown = vm.ContentMarkdown,
            CategoryId = vm.CategoryId,
            CoverMediaAssetId = vm.CoverMediaAssetId,
            IsPublished = vm.IsPublished && !vm.ScheduledPublishAtUtc.HasValue,
            ScheduledPublishAtUtc = vm.ScheduledPublishAtUtc,
            ExpiresAtUtc = vm.ExpiresAtUtc,
            IsFeatured = vm.IsFeatured,
            IsSticky = vm.IsSticky,
            IsPremium = vm.IsPremium,
            IsSponsored = vm.IsSponsored,
            SponsoredLabel = vm.SponsoredLabel?.Trim(),
            ReadingTimeMinutes = _markdown.EstimateReadingTimeMinutes(vm.ContentMarkdown),
            PublishedAtUtc = (vm.IsPublished && !vm.ScheduledPublishAtUtc.HasValue) ? DateTime.UtcNow : null,
            LanguageCode = lang,
            TranslationStatus = TranslationStatus.Original
        };
        await ApplyTagsAsync(post, vm.TagsCsv);
        _db.Posts.Add(post);
        await _db.SaveChangesAsync();
        post.TranslationGroupId = post.Id;
        await _db.SaveChangesAsync();
        await SaveRevisionAsync(post, authorId, "initial");

        try
        {
            await _events.PublishAsync(new PostCreatedDomainEvent(post.Id, post.Title, post.Slug, post.AuthorId));
            if (post.IsPublished && post.PublishedAtUtc.HasValue)
                await _events.PublishAsync(new PostPublishedDomainEvent(post.Id, post.Title, post.Slug, post.AuthorId, post.PublishedAtUtc.Value));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Domain event publish failed after Create PostId={Id}", post.Id);
        }

        return Redirect($"/{post.LanguageCode}/post/{post.Slug}");
    }

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
        if (id != vm.Id) return BadRequest();
        var post = await _db.Posts.Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (post is null) return NotFound();
        if (!AuthorAccess.OwnsPost(User, post)) return Forbid();

        if (!ModelState.IsValid)
        {
            vm.AvailableCategories = await GetCategoryOptionsAsync();
            return View(vm);
        }

        var authorId = AuthorAccess.UserId(User)!;
        var wasPublished = post.IsPublished;
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
        post.ExpiresAtUtc = vm.ExpiresAtUtc;
        post.ReadingTimeMinutes = _markdown.EstimateReadingTimeMinutes(vm.ContentMarkdown);
        post.UpdatedAtUtc = DateTime.UtcNow;
        post.TranslationStatus = vm.TranslationStatus;
        if (vm.ScheduledPublishAtUtc.HasValue && vm.ScheduledPublishAtUtc > DateTime.UtcNow)
        {
            post.IsPublished = false;
            post.ScheduledPublishAtUtc = vm.ScheduledPublishAtUtc;
        }
        else
        {
            post.IsPublished = vm.IsPublished;
            post.ScheduledPublishAtUtc = null;
            if (!wasPublished && vm.IsPublished) post.PublishedAtUtc = DateTime.UtcNow;
        }
        _db.PostTags.RemoveRange(post.PostTags);
        await ApplyTagsAsync(post, vm.TagsCsv);
        await _db.SaveChangesAsync();
        if (changed) await SaveRevisionAsync(post, authorId, "after-edit");

        try
        {
            if (!wasPublished && post.IsPublished && post.PublishedAtUtc.HasValue)
                await _events.PublishAsync(new PostPublishedDomainEvent(post.Id, post.Title, post.Slug, post.AuthorId, post.PublishedAtUtc.Value));
            else if (wasPublished && !post.IsPublished)
                await _events.PublishAsync(new PostUnpublishedDomainEvent(post.Id, post.Slug));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Domain event publish failed after Edit PostId={Id}", post.Id);
        }

        return Redirect($"/{post.LanguageCode}/post/{post.Slug}");
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
