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

        // Unpublished: only the author or SuperAdmin (CanManageAllPosts)
        if (!post.IsPublished && !AuthorAccess.OwnsPost(User, post))
            return NotFound();

        try
        {
            await _analytics.TrackPostViewAsync(HttpContext, post);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TrackPostView failed PostId={Id}", post.Id);
            try
            {
                post.ViewCount++;
                await _db.SaveChangesAsync();
            }
            catch { /* ignore */ }
        }

        ViewBag.RenderedHtml = _markdown.RenderToHtmlWithToc(
            post.ContentMarkdown ?? string.Empty,
            includeToc: false,
            cultureCode: post.LanguageCode);
        ViewBag.TocHtml = _markdown.GenerateTableOfContents(
            post.ContentMarkdown ?? string.Empty,
            "post-toc",
            post.LanguageCode);
        ViewBag.ReadingTimeMinutes = Math.Max(1, post.ReadingTimeMinutes);
        ViewBag.CommentSort = string.Equals(sort, "latest", StringComparison.OrdinalIgnoreCase) ? "latest" : "relevant";
        ViewBag.CanEdit = AuthorAccess.OwnsPost(User, post);
        ViewBag.CurrentUserId = AuthorAccess.UserId(User);
        ViewData["Title"] = post.Title;

        try { await LoadTaxonomyContextAsync(post); } catch (Exception ex) { _logger.LogDebug(ex, "Taxonomy context"); }
        try { await LoadSocialContextAsync(post); } catch (Exception ex) { _logger.LogDebug(ex, "Social context"); }

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
        if (vm.Id > 0)
            return await Edit(vm.Id, vm);

        if (string.IsNullOrWhiteSpace(vm.ContentMarkdown)
            && Request.Form.TryGetValue("ContentMarkdown", out var rawBody)
            && !string.IsNullOrWhiteSpace(rawBody))
        {
            vm.ContentMarkdown = rawBody.ToString();
            ModelState.Remove(nameof(vm.ContentMarkdown));
        }

        if (string.IsNullOrWhiteSpace(vm.ContentMarkdown))
            ModelState.AddModelError(nameof(vm.ContentMarkdown), "محتوای نوشته الزامی است");

        if (!ModelState.IsValid)
        {
            vm.AvailableCategories = await GetCategoryOptionsAsync();
            return View(vm);
        }

        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;

        var authorId = AuthorAccess.UserId(User)!;
        var lang = AppCultures.Normalize(vm.LanguageCode);
        var uniqueSlug = await MakeUniqueSlugAsync(SlugHelper.Slugify(vm.Title), lang);
        var wantPublish = ResolvePublishFlag(vm);

        var post = new Post
        {
            Title = vm.Title,
            AuthorId = authorId,
            Slug = uniqueSlug,
            Summary = string.IsNullOrWhiteSpace(vm.Summary) ? _ai.Summarize(vm.ContentMarkdown) : vm.Summary,
            ContentMarkdown = vm.ContentMarkdown,
            CategoryId = vm.CategoryId,
            CoverMediaAssetId = vm.CoverMediaAssetId,
            IsFeatured = vm.IsFeatured,
            IsSticky = vm.IsSticky,
            IsPremium = vm.IsPremium,
            IsSponsored = vm.IsSponsored,
            SponsoredLabel = vm.SponsoredLabel?.Trim(),
            ReadingTimeMinutes = _markdown.EstimateReadingTimeMinutes(vm.ContentMarkdown),
            LanguageCode = lang,
            TranslationStatus = TranslationStatus.Original
        };

        ApplyPublishState(post, wantPublish, vm.ScheduledPublishAtUtc, vm.ExpiresAtUtc, wasPublished: false);

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
                _logger.LogWarning(
                    "Edit PostId={Id}: empty ContentMarkdown in form — kept existing body ({Len} chars)",
                    id, post.ContentMarkdown.Length);
            }
            else
            {
                var lastRev = await _db.PostRevisions.AsNoTracking()
                    .Where(r => r.PostId == id && r.ContentMarkdown != null && r.ContentMarkdown != "")
                    .OrderByDescending(r => r.CreatedAtUtc)
                    .Select(r => r.ContentMarkdown)
                    .FirstOrDefaultAsync();
                if (!string.IsNullOrWhiteSpace(lastRev))
                {
                    vm.ContentMarkdown = lastRev;
                    ModelState.Remove(nameof(vm.ContentMarkdown));
                }
                else
                {
                    ModelState.AddModelError(nameof(vm.ContentMarkdown), "محتوای نوشته الزامی است");
                }
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

        _logger.LogInformation(
            "Edit saved PostId={Id} Published={Pub} Scheduled={Sched} BodyLen={Len}",
            post.Id, post.IsPublished, post.ScheduledPublishAtUtc, post.ContentMarkdown?.Length ?? 0);

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
