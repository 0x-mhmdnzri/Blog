using BlogApp.Data;
using BlogApp.Developer.Domain;
using BlogApp.Developer.Messaging;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using BlogApp.Services.Analytics;
using BlogApp.Services.Messaging;
using BlogApp.Services.Seo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public partial class PostsController
{
    [HttpGet("post/{slug}")]
    [OutputCache(PolicyName = "post")]
    public async Task<IActionResult> Details(string slug, string? sort = null)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 220)
            return NotFound();

        // P0.2: scheduling is handled by ContentScheduleHostedService; skip on crawler hits
        var ua = HttpContext.Request.Headers.UserAgent.ToString();
        var isBot = BotDetector.TryMatch(ua, out _);
        if (!isBot)
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

        ApplyPostSeo(post);

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
            LanguageCode = _culture.CurrentCode,
            TranslationStatus = TranslationStatus.Original
        };
        await LoadTaxonomyPickListsAsync(vm);
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
            await LoadTaxonomyPickListsAsync(vm);
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
        await ApplyFoldersAndSeriesAsync(post, vm);

        if (!AuthorAccess.IsSuperAdmin(User))
        {
            post.IsPublished = false;
            post.ScheduledPublishAtUtc = null;
            post.ReviewStatus = PostReviewStatus.PendingReview;
            await _db.SaveChangesAsync();
        }

        if (post.ReviewStatus == PostReviewStatus.PendingReview)
            await NotifySuperAdminsPendingPostAsync(post);

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

        TempData["PostCreatedPending"] = post.ReviewStatus == PostReviewStatus.PendingReview
            ? "1"
            : "0";
        TempData["PostCreatedTitle"] = post.Title;
        return RedirectToAction("Posts", "Admin");
    }
}
