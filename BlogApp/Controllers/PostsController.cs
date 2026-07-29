using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public class PostsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly MarkdownService _markdown;
    private readonly SeoService _seo;
    private readonly AnalyticsBroadcaster _broadcaster;

    public PostsController(ApplicationDbContext db, MarkdownService markdown, SeoService seo, AnalyticsBroadcaster broadcaster)
    {
        _db = db;
        _markdown = markdown;
        _seo = seo;
        _broadcaster = broadcaster;
    }

    [HttpGet("post/{slug}")]
    public async Task<IActionResult> Details(string slug)
    {
        var post = await _db.Posts
            .Include(p => p.Category)
            .Include(p => p.Author)
            .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
            .Include(p => p.Comments.Where(c => c.Status == CommentStatus.Approved))
            .FirstOrDefaultAsync(p => p.Slug == slug);

        if (post is null || (!post.IsPublished && !User.Identity!.IsAuthenticated))
            return NotFound();

        // Drafts: only owner or SuperAdmin can view
        if (!post.IsPublished && !AuthorAccess.OwnsPost(User, post))
            return NotFound();

        var visitorHash = VisitorIdentity.ComputeHash(HttpContext);
        var dedupWindowStart = DateTime.UtcNow.AddMinutes(-30);
        var isDuplicateVisit = await _db.PostViews.AnyAsync(v =>
            v.PostId == post.Id && v.VisitorHash == visitorHash && v.ViewedAtUtc >= dedupWindowStart);

        if (!isDuplicateVisit)
        {
            post.ViewCount++;
            var postView = new PostView { PostId = post.Id, ViewedAtUtc = DateTime.UtcNow, VisitorHash = visitorHash };
            _db.PostViews.Add(postView);
            await _db.SaveChangesAsync();

            _broadcaster.Publish(new
            {
                type = "view",
                postId = post.Id,
                postTitle = post.Title,
                authorId = post.AuthorId,
                viewedAtUtc = postView.ViewedAtUtc,
                totalViews = post.ViewCount
            });
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var canonicalUrl = $"{baseUrl}/post/{post.Slug}";
        string? imageUrl = post.CoverMediaAssetId is int coverId ? $"{baseUrl}/media/{coverId}" : null;

        ViewData["Title"] = post.Title;
        ViewData["Description"] = post.Summary;
        ViewData["OgType"] = "article";
        ViewData["Canonical"] = canonicalUrl;
        ViewData["OgImage"] = imageUrl;

        ViewBag.PostJsonLd = _seo.BuildPostJsonLd(post, canonicalUrl, imageUrl);
        ViewBag.BreadcrumbJsonLd = _seo.BuildBreadcrumbJsonLd(
            ("Home", baseUrl + "/"),
            post.Category != null ? (post.Category.Name, $"{baseUrl}/?category={post.Category.Slug}") : ("Posts", baseUrl + "/"),
            (post.Title, canonicalUrl));

        ViewBag.RenderedHtml = _markdown.RenderToHtml(post.ContentMarkdown);
        ViewBag.CanEdit = AuthorAccess.OwnsPost(User, post);
        return View(post);
    }

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    public async Task<IActionResult> Create()
    {
        var vm = new PostEditViewModel { AvailableCategories = await GetCategoryOptionsAsync() };
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

        var post = new Post
        {
            Title = vm.Title,
            AuthorId = authorId,
            Slug = await MakeUniqueSlugAsync(SlugHelper.Slugify(vm.Title)),
            Summary = string.IsNullOrWhiteSpace(vm.Summary)
                ? _markdown.ToPlainText(vm.ContentMarkdown)
                : vm.Summary,
            ContentMarkdown = vm.ContentMarkdown,
            CategoryId = vm.CategoryId,
            CoverMediaAssetId = vm.CoverMediaAssetId,
            IsPublished = vm.IsPublished,
            PublishedAtUtc = vm.IsPublished ? DateTime.UtcNow : null,
        };

        await ApplyTagsAsync(post, vm.TagsCsv);

        _db.Posts.Add(post);
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { slug = post.Slug });
    }

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    public async Task<IActionResult> Edit(int id)
    {
        var post = await _db.Posts.Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (post is null) return NotFound();
        if (!AuthorAccess.OwnsPost(User, post)) return Forbid();

        var vm = new PostEditViewModel
        {
            Id = post.Id,
            Title = post.Title,
            Summary = post.Summary,
            ContentMarkdown = post.ContentMarkdown,
            CategoryId = post.CategoryId,
            TagsCsv = string.Join(", ", post.PostTags.Select(pt => pt.Tag.Name)),
            IsPublished = post.IsPublished,
            CoverMediaAssetId = post.CoverMediaAssetId,
            AvailableCategories = await GetCategoryOptionsAsync()
        };
        return View(vm);
    }

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PostEditViewModel vm)
    {
        var post = await _db.Posts.Include(p => p.PostTags).FirstOrDefaultAsync(p => p.Id == vm.Id);
        if (post is null) return NotFound();
        if (!AuthorAccess.OwnsPost(User, post)) return Forbid();

        if (!ModelState.IsValid)
        {
            vm.AvailableCategories = await GetCategoryOptionsAsync();
            return View(vm);
        }

        var wasPublished = post.IsPublished;

        post.Title = vm.Title;
        post.Summary = string.IsNullOrWhiteSpace(vm.Summary) ? _markdown.ToPlainText(vm.ContentMarkdown) : vm.Summary;
        post.ContentMarkdown = vm.ContentMarkdown;
        post.CategoryId = vm.CategoryId;
        post.CoverMediaAssetId = vm.CoverMediaAssetId;
        post.IsPublished = vm.IsPublished;
        post.UpdatedAtUtc = DateTime.UtcNow;
        if (!wasPublished && vm.IsPublished) post.PublishedAtUtc = DateTime.UtcNow;

        _db.PostTags.RemoveRange(post.PostTags);
        await ApplyTagsAsync(post, vm.TagsCsv);

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { slug = post.Slug });
    }

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
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
    public IActionResult PreviewMarkdown([FromForm] string content)
    {
        return Content(_markdown.RenderToHtml(content ?? string.Empty), "text/html");
    }

    [HttpPost, ValidateAntiForgeryToken, AllowAnonymous]
    public async Task<IActionResult> AddComment(int postId, string authorName, string body)
    {
        if (!string.IsNullOrWhiteSpace(authorName) && !string.IsNullOrWhiteSpace(body))
        {
            var comment = new Comment
            {
                PostId = postId,
                AuthorName = authorName.Trim(),
                Body = body.Trim(),
                Status = CommentStatus.Pending
            };
            _db.Comments.Add(comment);
            await _db.SaveChangesAsync();
            TempData["CommentSubmitted"] = "ممنون — دیدگاه شما در انتظار بررسی است.";

            var postInfo = await _db.Posts.Where(p => p.Id == postId)
                .Select(p => new { p.Title, p.AuthorId }).FirstOrDefaultAsync();
            _broadcaster.Publish(new
            {
                type = "comment",
                status = "pending",
                postId,
                postTitle = postInfo?.Title,
                authorId = postInfo?.AuthorId,
                authorName = comment.AuthorName
            });
        }

        var slug = await _db.Posts.Where(p => p.Id == postId).Select(p => p.Slug).FirstOrDefaultAsync();
        return RedirectToAction(nameof(Details), new { slug });
    }

    private async Task<List<CategoryOption>> GetCategoryOptionsAsync() =>
        await _db.Categories.OrderBy(c => c.Name)
            .Select(c => new CategoryOption { Id = c.Id, Name = c.Name })
            .ToListAsync();

    private async Task<string> MakeUniqueSlugAsync(string baseSlug)
    {
        var slug = baseSlug;
        var i = 2;
        while (await _db.Posts.AnyAsync(p => p.Slug == slug))
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
            var slug = SlugHelper.Slugify(name);
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
