using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public partial class PostsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly MarkdownService _markdown;
    private readonly SeoService _seo;
    private readonly AnalyticsBroadcaster _broadcaster;
    private readonly AiContentService _ai;

    public PostsController(ApplicationDbContext db, MarkdownService markdown, SeoService seo, AnalyticsBroadcaster broadcaster, AiContentService ai)
    {
        _db = db; _markdown = markdown; _seo = seo; _broadcaster = broadcaster; _ai = ai;
    }

    [HttpGet("post/{slug}")]
    public async Task<IActionResult> Details(string slug)
    {
        await ApplyScheduledAndExpirationAsync();
        var post = await _db.Posts.Include(p => p.Category).Include(p => p.Author)
            .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
            .Include(p => p.Comments.Where(c => c.Status == CommentStatus.Approved))
            .FirstOrDefaultAsync(p => p.Slug == slug && !p.IsDeleted);
        if (post is null || (!post.IsPublished && !User.Identity!.IsAuthenticated)) return NotFound();
        if (!post.IsPublished && !AuthorAccess.OwnsPost(User, post)) return NotFound();
        if (post.ExpiresAtUtc.HasValue && post.ExpiresAtUtc <= DateTime.UtcNow && !AuthorAccess.OwnsPost(User, post)) return NotFound();

        // Real public traffic only: never count the author/admin previewing their own post.
        var isStaffPreview = User.Identity?.IsAuthenticated == true
            && (User.IsInRole(AppRoles.Author) || User.IsInRole(AppRoles.SuperAdmin));

        if (!isStaffPreview)
        {
            var visitorHash = VisitorIdentity.ComputeHash(HttpContext);
            var window = DateTime.UtcNow.AddMinutes(-30);
            if (!await _db.PostViews.AnyAsync(v => v.PostId == post.Id && v.VisitorHash == visitorHash && v.ViewedAtUtc >= window))
            {
                post.ViewCount++;
                var pv = new PostView { PostId = post.Id, ViewedAtUtc = DateTime.UtcNow, VisitorHash = visitorHash };
                _db.PostViews.Add(pv);
                await _db.SaveChangesAsync();
                _broadcaster.Publish(new
                {
                    type = "view",
                    postId = post.Id,
                    postTitle = post.Title,
                    authorId = post.AuthorId,
                    viewedAtUtc = pv.ViewedAtUtc,
                    totalViews = post.ViewCount
                });
            }
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var canonicalUrl = $"{baseUrl}/post/{post.Slug}";
        string? imageUrl = post.CoverMediaAssetId is int cid ? $"{baseUrl}/media/{cid}" : null;
        ViewData["Title"] = post.Title;
        ViewData["Description"] = post.Summary;
        ViewData["OgType"] = "article";
        ViewData["Canonical"] = canonicalUrl;
        ViewData["OgImage"] = imageUrl;
        ViewBag.PostJsonLd = _seo.BuildPostJsonLd(post, canonicalUrl, imageUrl);
        ViewBag.BreadcrumbJsonLd = _seo.BuildBreadcrumbJsonLd(("Home", baseUrl + "/"), post.Category != null ? (post.Category.Name, $"{baseUrl}/?category={post.Category.Slug}") : ("Posts", baseUrl + "/"), (post.Title, canonicalUrl));
        ViewBag.RenderedHtml = _markdown.RenderToHtmlWithToc(post.ContentMarkdown, true);
        ViewBag.ReadingTimeMinutes = post.ReadingTimeMinutes > 0 ? post.ReadingTimeMinutes : _markdown.EstimateReadingTimeMinutes(post.ContentMarkdown);
        ViewBag.CanEdit = AuthorAccess.OwnsPost(User, post);
        return View(post);
    }

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    public async Task<IActionResult> Create() =>
        View(new PostEditViewModel { AvailableCategories = await GetCategoryOptionsAsync() });

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PostEditViewModel vm)
    {
        if (!ModelState.IsValid) { vm.AvailableCategories = await GetCategoryOptionsAsync(); return View(vm); }
        var authorId = AuthorAccess.UserId(User)!;
        var post = new Post
        {
            Title = vm.Title, AuthorId = authorId,
            Slug = await MakeUniqueSlugAsync(SlugHelper.Slugify(vm.Title)),
            Summary = string.IsNullOrWhiteSpace(vm.Summary) ? _ai.Summarize(vm.ContentMarkdown) : vm.Summary,
            ContentMarkdown = vm.ContentMarkdown, CategoryId = vm.CategoryId, CoverMediaAssetId = vm.CoverMediaAssetId,
            IsPublished = vm.IsPublished && !vm.ScheduledPublishAtUtc.HasValue,
            ScheduledPublishAtUtc = vm.ScheduledPublishAtUtc, ExpiresAtUtc = vm.ExpiresAtUtc,
            IsFeatured = vm.IsFeatured, IsSticky = vm.IsSticky,
            ReadingTimeMinutes = _markdown.EstimateReadingTimeMinutes(vm.ContentMarkdown),
            PublishedAtUtc = (vm.IsPublished && !vm.ScheduledPublishAtUtc.HasValue) ? DateTime.UtcNow : null
        };
        await ApplyTagsAsync(post, vm.TagsCsv);
        _db.Posts.Add(post);
        await _db.SaveChangesAsync();
        await SaveRevisionAsync(post, authorId, "initial");
        return RedirectToAction(nameof(Details), new { slug = post.Slug });
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
            Id = post.Id, Title = post.Title, Summary = post.Summary, ContentMarkdown = post.ContentMarkdown,
            CategoryId = post.CategoryId, TagsCsv = string.Join(", ", post.PostTags.Select(pt => pt.Tag.Name)),
            IsPublished = post.IsPublished, ScheduledPublishAtUtc = post.ScheduledPublishAtUtc, ExpiresAtUtc = post.ExpiresAtUtc,
            IsFeatured = post.IsFeatured, IsSticky = post.IsSticky, CoverMediaAssetId = post.CoverMediaAssetId,
            ReadingTimeMinutes = post.ReadingTimeMinutes, AvailableCategories = await GetCategoryOptionsAsync(),
            Revisions = post.Revisions.Select(r => new PostRevisionItem { Id = r.Id, Title = r.Title, CreatedAtUtc = r.CreatedAtUtc, Note = r.Note }).ToList()
        });
    }

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PostEditViewModel vm)
    {
        var post = await _db.Posts.Include(p => p.PostTags).FirstOrDefaultAsync(p => p.Id == vm.Id && !p.IsDeleted);
        if (post is null) return NotFound();
        if (!AuthorAccess.OwnsPost(User, post)) return Forbid();
        if (!ModelState.IsValid) { vm.AvailableCategories = await GetCategoryOptionsAsync(); return View(vm); }
        var authorId = AuthorAccess.UserId(User)!;
        var wasPublished = post.IsPublished;
        var changed = post.ContentMarkdown != vm.ContentMarkdown || post.Title != vm.Title;
        if (changed) await SaveRevisionAsync(post, authorId, "before-edit");
        post.Title = vm.Title;
        post.Summary = string.IsNullOrWhiteSpace(vm.Summary) ? _ai.Summarize(vm.ContentMarkdown) : vm.Summary;
        post.ContentMarkdown = vm.ContentMarkdown;
        post.CategoryId = vm.CategoryId; post.CoverMediaAssetId = vm.CoverMediaAssetId;
        post.IsFeatured = vm.IsFeatured; post.IsSticky = vm.IsSticky;
        post.ExpiresAtUtc = vm.ExpiresAtUtc;
        post.ReadingTimeMinutes = _markdown.EstimateReadingTimeMinutes(vm.ContentMarkdown);
        post.UpdatedAtUtc = DateTime.UtcNow;
        if (vm.ScheduledPublishAtUtc.HasValue && vm.ScheduledPublishAtUtc > DateTime.UtcNow)
        { post.IsPublished = false; post.ScheduledPublishAtUtc = vm.ScheduledPublishAtUtc; }
        else
        {
            post.IsPublished = vm.IsPublished; post.ScheduledPublishAtUtc = null;
            if (!wasPublished && vm.IsPublished) post.PublishedAtUtc = DateTime.UtcNow;
        }
        _db.PostTags.RemoveRange(post.PostTags);
        await ApplyTagsAsync(post, vm.TagsCsv);
        await _db.SaveChangesAsync();
        if (changed) await SaveRevisionAsync(post, authorId, "after-edit");
        return RedirectToAction(nameof(Details), new { slug = post.Slug });
    }

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult PreviewMarkdown([FromForm] string content) =>
        Content(_markdown.RenderToHtmlWithToc(content ?? "", false), "text/html");

    [HttpPost, ValidateAntiForgeryToken, AllowAnonymous]
    public async Task<IActionResult> AddComment(int postId, string authorName, string body)
    {
        if (!string.IsNullOrWhiteSpace(authorName) && !string.IsNullOrWhiteSpace(body))
        {
            var comment = new Comment { PostId = postId, AuthorName = authorName.Trim(), Body = body.Trim(), Status = CommentStatus.Pending };
            _db.Comments.Add(comment);
            await _db.SaveChangesAsync();
            TempData["CommentSubmitted"] = "ممنون — دیدگاه شما در انتظار بررسی است.";
            var info = await _db.Posts.Where(p => p.Id == postId).Select(p => new { p.Title, p.AuthorId }).FirstOrDefaultAsync();
            _broadcaster.Publish(new { type = "comment", status = "pending", postId, postTitle = info?.Title, authorId = info?.AuthorId, authorName = comment.AuthorName });
        }
        var slug = await _db.Posts.Where(p => p.Id == postId).Select(p => p.Slug).FirstOrDefaultAsync();
        return RedirectToAction(nameof(Details), new { slug });
    }
}
