using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

/// <summary>Admin + public endpoints for categories, tags, series, topic collections.</summary>
[Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
public class TaxonomyController : Controller
{
    private readonly ApplicationDbContext _db;

    public TaxonomyController(ApplicationDbContext db) => _db = db;

    // ─── Admin: Categories ───────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Categories()
    {
        ViewData["Title"] = "دسته‌بندی‌ها";
        var cats = await _db.Categories
            .Include(c => c.Children)
            .Include(c => c.Posts)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .ToListAsync();

        var roots = cats.Where(c => c.ParentId == null).ToList();
        var vm = new TaxonomyAdminViewModel
        {
            Categories = FlattenTree(roots, 0),
            ParentOptions = cats.Select(c => new CategoryOption { Id = c.Id, Name = Indent(c) }).ToList(),
            Tags = await _db.Tags.OrderBy(t => t.Name)
                .Select(t => new TagAdminItem
                {
                    Id = t.Id, Name = t.Name, Slug = t.Slug, Description = t.Description,
                    PostCount = t.PostTags.Count
                }).ToListAsync(),
            Series = await _db.PostSeries.OrderBy(s => s.Name)
                .Select(s => new SeriesAdminItem
                {
                    Id = s.Id, Name = s.Name, Slug = s.Slug, Description = s.Description,
                    PostCount = s.Posts.Count
                }).ToListAsync(),
            Topics = await _db.TopicCollections.OrderBy(t => t.Name)
                .Select(t => new TopicAdminItem
                {
                    Id = t.Id, Name = t.Name, Slug = t.Slug, Description = t.Description,
                    IsPublished = t.IsPublished, ItemCount = t.Items.Count
                }).ToListAsync()
        };
        return View("~/Views/Admin/Taxonomy.cshtml", vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(string name, string? description, int? parentId)
    {
        if (string.IsNullOrWhiteSpace(name)) return RedirectToAction(nameof(Categories));
        var slug = await UniqueCategorySlugAsync(SlugHelper.Slugify(name));
        _db.Categories.Add(new Category
        {
            Name = name.Trim(),
            Slug = slug,
            Description = description?.Trim(),
            ParentId = parentId,
            DisplayOrder = await _db.Categories.CountAsync()
        });
        await _db.SaveChangesAsync();
        TempData["TaxonomyMsg"] = "دسته افزوده شد.";
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCategory(int id, string name, string? description, int? parentId)
    {
        var cat = await _db.Categories.FindAsync(id);
        if (cat is null) return NotFound();
        if (parentId == id) parentId = cat.ParentId; // prevent self-parent
        if (parentId.HasValue && await IsDescendantAsync(id, parentId.Value))
            parentId = cat.ParentId; // prevent cycle

        cat.Name = name.Trim();
        cat.Description = description?.Trim();
        cat.ParentId = parentId;
        if (!string.Equals(SlugHelper.Slugify(name), cat.Slug, StringComparison.OrdinalIgnoreCase))
            cat.Slug = await UniqueCategorySlugAsync(SlugHelper.Slugify(name), id);
        await _db.SaveChangesAsync();
        TempData["TaxonomyMsg"] = "دسته به‌روز شد.";
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var cat = await _db.Categories.Include(c => c.Children).FirstOrDefaultAsync(c => c.Id == id);
        if (cat is null) return NotFound();
        if (cat.Children.Any())
        {
            TempData["TaxonomyErr"] = "ابتدا زیردسته‌ها را حذف یا جابه‌جا کنید.";
            return RedirectToAction(nameof(Categories));
        }
        var posts = await _db.Posts.Where(p => p.CategoryId == id).ToListAsync();
        foreach (var p in posts) p.CategoryId = null;
        _db.Categories.Remove(cat);
        await _db.SaveChangesAsync();
        TempData["TaxonomyMsg"] = "دسته حذف شد.";
        return RedirectToAction(nameof(Categories));
    }

    // ─── Admin: Tags ─────────────────────────────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTag(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name)) return RedirectToAction(nameof(Categories));
        var slug = await UniqueTagSlugAsync(SlugHelper.Slugify(name));
        _db.Tags.Add(new Tag { Name = name.Trim(), Slug = slug, Description = description?.Trim() });
        await _db.SaveChangesAsync();
        TempData["TaxonomyMsg"] = "برچسب افزوده شد.";
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTag(int id, string name, string? description)
    {
        var tag = await _db.Tags.FindAsync(id);
        if (tag is null) return NotFound();
        tag.Name = name.Trim();
        tag.Description = description?.Trim();
        if (!string.Equals(SlugHelper.Slugify(name), tag.Slug, StringComparison.OrdinalIgnoreCase))
            tag.Slug = await UniqueTagSlugAsync(SlugHelper.Slugify(name), id);
        await _db.SaveChangesAsync();
        TempData["TaxonomyMsg"] = "برچسب به‌روز شد.";
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTag(int id)
    {
        var tag = await _db.Tags.FindAsync(id);
        if (tag is null) return NotFound();
        var links = await _db.PostTags.Where(pt => pt.TagId == id).ToListAsync();
        _db.PostTags.RemoveRange(links);
        _db.Tags.Remove(tag);
        await _db.SaveChangesAsync();
        TempData["TaxonomyMsg"] = "برچسب حذف شد.";
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MergeTags(int sourceId, int targetId)
    {
        if (sourceId == targetId) return RedirectToAction(nameof(Categories));
        var sourceLinks = await _db.PostTags.Where(pt => pt.TagId == sourceId).ToListAsync();
        var targetPostIds = await _db.PostTags.Where(pt => pt.TagId == targetId).Select(pt => pt.PostId).ToListAsync();
        foreach (var link in sourceLinks)
        {
            if (targetPostIds.Contains(link.PostId))
                _db.PostTags.Remove(link);
            else
                link.TagId = targetId;
        }
        var source = await _db.Tags.FindAsync(sourceId);
        if (source is not null) _db.Tags.Remove(source);
        await _db.SaveChangesAsync();
        TempData["TaxonomyMsg"] = "برچسب‌ها ادغام شدند.";
        return RedirectToAction(nameof(Categories));
    }

    // ─── Admin: Series ───────────────────────────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSeries(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name)) return RedirectToAction(nameof(Categories));
        var slug = await UniqueSeriesSlugAsync(SlugHelper.Slugify(name));
        _db.PostSeries.Add(new PostSeries { Name = name.Trim(), Slug = slug, Description = description?.Trim() });
        await _db.SaveChangesAsync();
        TempData["TaxonomyMsg"] = "مجموعه (سری) ایجاد شد.";
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSeries(int id)
    {
        var s = await _db.PostSeries.FindAsync(id);
        if (s is null) return NotFound();
        _db.PostSeries.Remove(s);
        await _db.SaveChangesAsync();
        TempData["TaxonomyMsg"] = "سری حذف شد.";
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPostToSeries(int seriesId, int postId)
    {
        if (!await _db.SeriesPosts.AnyAsync(sp => sp.SeriesId == seriesId && sp.PostId == postId))
        {
            var max = await _db.SeriesPosts.Where(sp => sp.SeriesId == seriesId).MaxAsync(sp => (int?)sp.SortOrder) ?? 0;
            _db.SeriesPosts.Add(new SeriesPost { SeriesId = seriesId, PostId = postId, SortOrder = max + 1 });
            await _db.SaveChangesAsync();
        }
        TempData["TaxonomyMsg"] = "نوشته به سری اضافه شد.";
        return RedirectToAction(nameof(EditSeries), new { id = seriesId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemovePostFromSeries(int seriesId, int postId)
    {
        var row = await _db.SeriesPosts.FirstOrDefaultAsync(sp => sp.SeriesId == seriesId && sp.PostId == postId);
        if (row is not null)
        {
            _db.SeriesPosts.Remove(row);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(EditSeries), new { id = seriesId });
    }

    [HttpGet]
    public async Task<IActionResult> EditSeries(int id)
    {
        var series = await _db.PostSeries
            .Include(s => s.Posts).ThenInclude(sp => sp.Post)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (series is null) return NotFound();
        ViewData["Title"] = "ویرایش سری: " + series.Name;
        var members = series.Posts.OrderBy(sp => sp.SortOrder).ToList();
        var memberIds = members.Select(m => m.PostId).ToHashSet();
        var available = await _db.Posts.Where(p => !p.IsDeleted && !memberIds.Contains(p.Id))
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new { p.Id, p.Title })
            .Take(100)
            .ToListAsync();
        ViewBag.Series = series;
        ViewBag.Members = members;
        ViewBag.AvailablePosts = available;
        return View("~/Views/Admin/EditSeries.cshtml");
    }

    // ─── Admin: Topic collections ────────────────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTopic(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name)) return RedirectToAction(nameof(Categories));
        var slug = await UniqueTopicSlugAsync(SlugHelper.Slugify(name));
        _db.TopicCollections.Add(new TopicCollection
        {
            Name = name.Trim(), Slug = slug, Description = description?.Trim(), IsPublished = true
        });
        await _db.SaveChangesAsync();
        TempData["TaxonomyMsg"] = "مجموعه موضوعی ایجاد شد.";
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTopic(int id)
    {
        var t = await _db.TopicCollections.FindAsync(id);
        if (t is null) return NotFound();
        _db.TopicCollections.Remove(t);
        await _db.SaveChangesAsync();
        TempData["TaxonomyMsg"] = "مجموعه موضوعی حذف شد.";
        return RedirectToAction(nameof(Categories));
    }

    [HttpGet]
    public async Task<IActionResult> EditTopic(int id)
    {
        var topic = await _db.TopicCollections
            .Include(t => t.Items).ThenInclude(i => i.Category)
            .Include(t => t.Items).ThenInclude(i => i.Tag)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (topic is null) return NotFound();
        ViewData["Title"] = "ویرایش موضوع: " + topic.Name;
        ViewBag.Topic = topic;
        ViewBag.AllCategories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
        ViewBag.AllTags = await _db.Tags.OrderBy(t => t.Name).ToListAsync();
        return View("~/Views/Admin/EditTopic.cshtml");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTopicItem(int topicId, int? categoryId, int? tagId)
    {
        if (categoryId is null && tagId is null) return RedirectToAction(nameof(EditTopic), new { id = topicId });
        var max = await _db.TopicCollectionItems.Where(i => i.TopicCollectionId == topicId).MaxAsync(i => (int?)i.SortOrder) ?? 0;
        _db.TopicCollectionItems.Add(new TopicCollectionItem
        {
            TopicCollectionId = topicId,
            CategoryId = categoryId,
            TagId = tagId,
            SortOrder = max + 1
        });
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(EditTopic), new { id = topicId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveTopicItem(int topicId, int itemId)
    {
        var item = await _db.TopicCollectionItems.FirstOrDefaultAsync(i => i.Id == itemId && i.TopicCollectionId == topicId);
        if (item is not null)
        {
            _db.TopicCollectionItems.Remove(item);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(EditTopic), new { id = topicId });
    }

    // ─── Public: Series & Topic landing ──────────────────────────────

    [AllowAnonymous]
    [HttpGet("/series/{slug}")]
    public async Task<IActionResult> Series(string slug)
    {
        var series = await _db.PostSeries
            .Include(s => s.Posts).ThenInclude(sp => sp.Post).ThenInclude(p => p.Category)
            .FirstOrDefaultAsync(s => s.Slug == slug);
        if (series is null) return NotFound();

        var posts = series.Posts
            .Where(sp => sp.Post is { IsDeleted: false, IsPublished: true })
            .OrderBy(sp => sp.SortOrder)
            .Select(sp => sp.Post)
            .ToList();

        ViewData["Title"] = series.Name;
        ViewData["Description"] = series.Description;
        ViewBag.Series = series;
        return View("~/Views/Taxonomy/Series.cshtml", posts);
    }

    [AllowAnonymous]
    [HttpGet("/topic/{slug}")]
    public async Task<IActionResult> Topic(string slug)
    {
        var topic = await _db.TopicCollections
            .Include(t => t.Items).ThenInclude(i => i.Category)
            .Include(t => t.Items).ThenInclude(i => i.Tag)
            .FirstOrDefaultAsync(t => t.Slug == slug && t.IsPublished);
        if (topic is null) return NotFound();

        var catIds = topic.Items.Where(i => i.CategoryId.HasValue).Select(i => i.CategoryId!.Value).ToList();
        var tagIds = topic.Items.Where(i => i.TagId.HasValue).Select(i => i.TagId!.Value).ToList();

        var posts = await _db.Posts
            .Where(p => !p.IsDeleted && p.IsPublished)
            .Where(p => (p.CategoryId != null && catIds.Contains(p.CategoryId.Value))
                        || p.PostTags.Any(pt => tagIds.Contains(pt.TagId)))
            .OrderByDescending(p => p.PublishedAtUtc)
            .Take(40)
            .Include(p => p.Category)
            .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
            .ToListAsync();

        ViewData["Title"] = topic.Name;
        ViewData["Description"] = topic.Description;
        ViewBag.Topic = topic;
        return View("~/Views/Taxonomy/Topic.cshtml", posts);
    }

    // ─── helpers ─────────────────────────────────────────────────────

    private static List<CategoryTreeItem> FlattenTree(IEnumerable<Category> roots, int depth)
    {
        var list = new List<CategoryTreeItem>();
        foreach (var c in roots.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name))
        {
            list.Add(new CategoryTreeItem
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                Description = c.Description,
                ParentId = c.ParentId,
                Depth = depth,
                PostCount = c.Posts.Count
            });
            if (c.Children.Any())
                list.AddRange(FlattenTree(c.Children, depth + 1));
        }
        return list;
    }

    private string Indent(Category c)
    {
        var depth = 0;
        var cur = c;
        // simple depth estimate from name only for option list — use Parent chain if loaded
        while (cur.ParentId != null) { depth++; break; }
        return (depth > 0 ? new string('—', depth) + " " : "") + c.Name;
    }

    private async Task<bool> IsDescendantAsync(int ancestorId, int nodeId)
    {
        var current = await _db.Categories.FindAsync(nodeId);
        var guard = 0;
        while (current?.ParentId != null && guard++ < 50)
        {
            if (current.ParentId == ancestorId) return true;
            current = await _db.Categories.FindAsync(current.ParentId);
        }
        return false;
    }

    private async Task<string> UniqueCategorySlugAsync(string baseSlug, int? excludeId = null)
    {
        var slug = baseSlug; var i = 2;
        while (await _db.Categories.AnyAsync(c => c.Slug == slug && c.Id != excludeId))
            slug = $"{baseSlug}-{i++}";
        return slug;
    }

    private async Task<string> UniqueTagSlugAsync(string baseSlug, int? excludeId = null)
    {
        var slug = baseSlug; var i = 2;
        while (await _db.Tags.AnyAsync(t => t.Slug == slug && t.Id != excludeId))
            slug = $"{baseSlug}-{i++}";
        return slug;
    }

    private async Task<string> UniqueSeriesSlugAsync(string baseSlug)
    {
        var slug = baseSlug; var i = 2;
        while (await _db.PostSeries.AnyAsync(s => s.Slug == slug))
            slug = $"{baseSlug}-{i++}";
        return slug;
    }

    private async Task<string> UniqueTopicSlugAsync(string baseSlug)
    {
        var slug = baseSlug; var i = 2;
        while (await _db.TopicCollections.AnyAsync(t => t.Slug == slug))
            slug = $"{baseSlug}-{i++}";
        return slug;
    }
}
