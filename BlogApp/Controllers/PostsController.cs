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

    public PostsController(ApplicationDbContext db, MarkdownService markdown)
    {
        _db = db;
        _markdown = markdown;
    }

    // GET /post/{slug} — public reading view
    [HttpGet("post/{slug}")]
    public async Task<IActionResult> Details(string slug)
    {
        var post = await _db.Posts
            .Include(p => p.Category)
            .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
            .Include(p => p.Comments.Where(c => c.IsApproved))
            .FirstOrDefaultAsync(p => p.Slug == slug);

        if (post is null || (!post.IsPublished && !User.Identity!.IsAuthenticated))
            return NotFound();

        post.ViewCount++;
        await _db.SaveChangesAsync();

        ViewBag.RenderedHtml = _markdown.RenderToHtml(post.ContentMarkdown);
        return View(post);
    }

    // GET /Posts/Create — author only
    [Authorize]
    public async Task<IActionResult> Create()
    {
        var vm = new PostEditViewModel { AvailableCategories = await GetCategoryOptionsAsync() };
        return View(vm);
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PostEditViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            vm.AvailableCategories = await GetCategoryOptionsAsync();
            return View(vm);
        }

        var post = new Post
        {
            Title = vm.Title,
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

    [Authorize]
    public async Task<IActionResult> Edit(int id)
    {
        var post = await _db.Posts.Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (post is null) return NotFound();

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

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PostEditViewModel vm)
    {
        var post = await _db.Posts.Include(p => p.PostTags).FirstOrDefaultAsync(p => p.Id == vm.Id);
        if (post is null) return NotFound();

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

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var post = await _db.Posts.FindAsync(id);
        if (post is null) return NotFound();
        _db.Posts.Remove(post); // cascades to media + comments
        await _db.SaveChangesAsync();
        return RedirectToAction("Index", "Home");
    }

    // POST /Posts/PreviewMarkdown — used by the live editor preview pane
    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public IActionResult PreviewMarkdown([FromForm] string content)
    {
        return Content(_markdown.RenderToHtml(content ?? string.Empty), "text/html");
    }

    [HttpPost, ValidateAntiForgeryToken, AllowAnonymous]
    public async Task<IActionResult> AddComment(int postId, string authorName, string body)
    {
        if (!string.IsNullOrWhiteSpace(authorName) && !string.IsNullOrWhiteSpace(body))
        {
            _db.Comments.Add(new Comment
            {
                PostId = postId,
                AuthorName = authorName.Trim(),
                Body = body.Trim(),
                IsApproved = false // moderated before showing publicly
            });
            await _db.SaveChangesAsync();
            TempData["CommentSubmitted"] = "Thanks — your comment is awaiting moderation.";
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
