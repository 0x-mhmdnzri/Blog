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
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public partial class PostsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly MarkdownService _markdown;
    private readonly SeoService _seo;
    private readonly AnalyticsBroadcaster _broadcaster;
    private readonly AiContentService _ai;
    private readonly INotificationService _notify;
    private readonly IAnalyticsTracker _analytics;
    private readonly ICultureService _culture;
    private readonly IDomainEventPublisher _events;
    private readonly ILogger<PostsController> _logger;

    public PostsController(
        ApplicationDbContext db,
        MarkdownService markdown,
        SeoService seo,
        AnalyticsBroadcaster broadcaster,
        AiContentService ai,
        INotificationService notify,
        IAnalyticsTracker analytics,
        ICultureService culture,
        IDomainEventPublisher events,
        ILogger<PostsController> logger)
    {
        _db = db;
        _markdown = markdown;
        _seo = seo;
        _broadcaster = broadcaster;
        _ai = ai;
        _notify = notify;
        _analytics = analytics;
        _culture = culture;
        _events = events;
        _logger = logger;
    }

    [HttpGet("post/{slug}")]
    public async Task<IActionResult> Details(string slug, string? sort = null)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 220)
            return NotFound();

        var commentSort = string.Equals(sort, "latest", StringComparison.OrdinalIgnoreCase)
            ? "latest"
            : "relevant";

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
        if (!post.IsPublished && !AuthorAccess.OwnsPost(User, post)) return NotFound();
        if (post.ExpiresAtUtc.HasValue && post.ExpiresAtUtc <= DateTime.UtcNow && !AuthorAccess.OwnsPost(User, post))
            return NotFound();

        if (!AuthorAccess.OwnsPost(User, post)
            && post.TranslationStatus is TranslationStatus.Draft or TranslationStatus.ReadyForReview)
            return NotFound();

        var isStaffPreview = User.Identity?.IsAuthenticated == true
            && (User.IsInRole(AppRoles.Author) || User.IsInRole(AppRoles.SuperAdmin));

        if (!isStaffPreview)
        {
            var before = post.ViewCount;
            await _analytics.TrackPostViewAsync(HttpContext, post);
            if (post.ViewCount > before)
            {
                _broadcaster.Publish(new
                {
                    type = "view",
                    postId = post.Id,
                    postSlug = post.Slug,
                    postTitle = post.Title,
                    authorId = post.AuthorId,
                    viewedAtUtc = DateTime.UtcNow,
                    totalViews = post.ViewCount
                });
            }
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var canonicalUrl = $"{baseUrl}/{post.LanguageCode}/post/{post.Slug}";
        string? imageUrl = post.CoverMediaAssetId is int cid ? $"{baseUrl}/media/{cid}" : null;
        ViewData["Title"] = post.Title;
        ViewData["Description"] = post.Summary;
        ViewData["OgType"] = "article";
        ViewData["Canonical"] = canonicalUrl;
        ViewData["OgImage"] = imageUrl;
        ViewBag.PostJsonLd = _seo.BuildPostJsonLd(post, canonicalUrl, imageUrl);
        ViewBag.BreadcrumbJsonLd = _seo.BuildBreadcrumbJsonLd(
            ("Home", baseUrl + "/" + post.LanguageCode + "/"),
            post.Category != null ? (post.Category.Name, $"{baseUrl}/{post.LanguageCode}/?category={post.Category.Slug}") : ("Posts", baseUrl + "/" + post.LanguageCode + "/"),
            (post.Title, canonicalUrl));

        ViewBag.RenderedHtml = _markdown.RenderToHtmlWithToc(post.ContentMarkdown, includeToc: false, cultureCode: post.LanguageCode);
        ViewBag.TocHtml = _markdown.GenerateTableOfContents(post.ContentMarkdown, "post-toc post-toc--sidebar", post.LanguageCode);

        await ApplyPremiumGateAsync(post);
        ViewBag.ReadingTimeMinutes = post.ReadingTimeMinutes > 0
            ? post.ReadingTimeMinutes
            : _markdown.EstimateReadingTimeMinutes(post.ContentMarkdown);
        ViewBag.CanEdit = AuthorAccess.OwnsPost(User, post);
        ViewBag.CommentSort = commentSort;

        var translations = await _culture.GetTranslationLinksAsync(post.Id);
        ViewBag.Translations = translations;
        ViewBag.HreflangLinks = translations
            .Where(t => t.IsPublished || AuthorAccess.OwnsPost(User, post))
            .Select(t => new
            {
                Lang = t.LanguageCode,
                Href = $"{baseUrl}/{t.LanguageCode}/post/{t.Slug}"
            }).ToList();

        var uid = AuthorAccess.UserId(User);
        ViewBag.IsBookmarked = uid != null
            && await _db.PostBookmarks.AnyAsync(b => b.UserId == uid && b.PostId == post.Id);

        if (uid != null)
        {
            var commentIds = post.Comments.Select(c => c.Id).ToList();
            ViewBag.LikedCommentIds = commentIds.Count == 0
                ? new HashSet<int>()
                : (await _db.CommentLikes
                    .Where(l => l.UserId == uid && commentIds.Contains(l.CommentId))
                    .Select(l => l.CommentId)
                    .ToListAsync()).ToHashSet();
        }
        else
        {
            ViewBag.LikedCommentIds = new HashSet<int>();
        }

        await LoadTaxonomyContextAsync(post);

        return View(post);
    }

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
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
        var post = new Post
        {
            Title = vm.Title,
            AuthorId = authorId,
            Slug = await MakeUniqueSlugAsync(SlugHelper.Slugify(vm.Title), lang),
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
            {
                await _events.PublishAsync(new PostPublishedDomainEvent(
                    post.Id, post.Title, post.Slug, post.AuthorId, post.PublishedAtUtc.Value));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Domain event publish failed after Create PostId={Id}", post.Id);
        }

        return Redirect($"/{post.LanguageCode}/post/{post.Slug}");
    }

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    public async Task<IActionResult> Edit(int id)
    {
        var post = await _db.Posts.Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
            .Include(p => p.Revisions.OrderByDescending(r => r.CreatedAtUtc).Take(20))
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (post is null) return NotFound();
        if (!AuthorAccess.OwnsPost(User, post)) return Forbid();
        return View(new PostEditViewModel
        {
            Id = post.Id,
            Title = post.Title,
            Summary = post.Summary,
            ContentMarkdown = post.ContentMarkdown,
            CategoryId = post.CategoryId,
            TagsCsv = string.Join(", ", post.PostTags.Select(pt => pt.Tag.Name)),
            IsPublished = post.IsPublished,
            ScheduledPublishAtUtc = post.ScheduledPublishAtUtc,
            ExpiresAtUtc = post.ExpiresAtUtc,
            IsFeatured = post.IsFeatured,
            IsSticky = post.IsSticky,
            IsPremium = post.IsPremium,
            IsSponsored = post.IsSponsored,
            SponsoredLabel = post.SponsoredLabel,
            CoverMediaAssetId = post.CoverMediaAssetId,
            ReadingTimeMinutes = post.ReadingTimeMinutes,
            LanguageCode = post.LanguageCode,
            TranslationStatus = post.TranslationStatus,
            TranslationGroupId = post.TranslationGroupId ?? post.Id,
            SiblingTranslations = await _culture.GetTranslationLinksAsync(post.Id),
            AvailableCategories = await GetCategoryOptionsAsync(),
            Revisions = post.Revisions.Select(r => new PostRevisionItem
            {
                Id = r.Id, Title = r.Title, CreatedAtUtc = r.CreatedAtUtc, Note = r.Note
            }).ToList()
        });
    }

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PostEditViewModel vm)
    {
        var post = await _db.Posts.Include(p => p.PostTags).FirstOrDefaultAsync(p => p.Id == vm.Id && !p.IsDeleted);
        if (post is null) return NotFound();
        if (!AuthorAccess.OwnsPost(User, post)) return Forbid();
        if (!ModelState.IsValid)
        {
            vm.AvailableCategories = await GetCategoryOptionsAsync();
            vm.SiblingTranslations = await _culture.GetTranslationLinksAsync(post.Id);
            return View(vm);
        }
        var authorId = AuthorAccess.UserId(User)!;
        var wasPublished = post.IsPublished;
        var changed = post.ContentMarkdown != vm.ContentMarkdown || post.Title != vm.Title;
        if (changed) await SaveRevisionAsync(post, authorId, "before-edit");
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
            {
                await _events.PublishAsync(new PostPublishedDomainEvent(
                    post.Id, post.Title, post.Slug, post.AuthorId, post.PublishedAtUtc.Value));
            }
            else if (wasPublished && !post.IsPublished)
            {
                await _events.PublishAsync(new PostUnpublishedDomainEvent(post.Id, post.Slug));
            }
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
            TempData["Error"] = "زبان پشتیبانی نمی‌شود.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        try
        {
            var draft = await _culture.CreateTranslationDraftAsync(source, targetLanguage, AuthorAccess.UserId(User)!);
            TempData["Saved"] = $"پیش‌نویس ترجمه ({AppCultures.Find(targetLanguage)?.NativeName}) ساخته شد.";
            return RedirectToAction(nameof(Edit), new { id = draft.Id });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CreateTranslation failed PostId={Id} Lang={Lang}", id, targetLanguage);
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Edit), new { id });
        }
    }

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult PreviewMarkdown([FromForm] string content) =>
        Content(_markdown.RenderToHtmlWithToc(content ?? "", false), "text/html");

    [HttpPost, ValidateAntiForgeryToken, AllowAnonymous]
    [EnableRateLimiting("comment")]
    public async Task<IActionResult> AddComment(int postId, string authorName, string body)
    {
        authorName = (authorName ?? string.Empty).Trim();
        body = (body ?? string.Empty).Trim();

        if (authorName.Length is < 2 or > 80 || body.Length is < 2 or > 2000)
        {
            TempData["CommentSubmitted"] = "نام یا متن دیدگاه معتبر نیست.";
            var bad = await _db.Posts.Where(p => p.Id == postId).Select(p => new { p.Slug, p.LanguageCode }).FirstOrDefaultAsync();
            return bad is null ? NotFound() : Redirect($"/{bad.LanguageCode}/post/{bad.Slug}");
        }

        authorName = new string(authorName.Where(c => !char.IsControl(c)).ToArray());
        body = new string(body.Where(c => c is '\n' or '\r' or '\t' || !char.IsControl(c)).ToArray());

        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == postId && p.IsPublished && !p.IsDeleted);
        if (post is null)
            return NotFound();

        var comment = new Comment
        {
            PostId = postId,
            AuthorName = authorName,
            Body = body,
            Status = CommentStatus.Pending
        };
        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();
        TempData["CommentSubmitted"] = "ممنون — دیدگاه شما در انتظار بررسی است.";

        try
        {
            await _notify.NotifyNewCommentAsync(post, comment);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notify new comment failed PostId={PostId}", postId);
        }

        _broadcaster.Publish(new
        {
            type = "comment",
            status = "pending",
            postId,
            postTitle = post.Title,
            authorId = post.AuthorId,
            authorName = comment.AuthorName
        });

        return Redirect($"/{post.LanguageCode}/post/{post.Slug}");
    }
}
