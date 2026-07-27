using BlogApp.Data;
using BlogApp.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;

    public HomeController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? category, string? tag, int page = 1)
    {
        const int pageSize = 8;
        var isAuthor = User.Identity?.IsAuthenticated == true;

        var query = _db.Posts
            .Where(p => p.IsPublished || isAuthor) // the author can see their own drafts too
            .Include(p => p.Category)
            .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
            .OrderByDescending(p => p.IsPublished ? p.PublishedAtUtc : p.CreatedAtUtc)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category != null && p.Category.Slug == category);

        if (!string.IsNullOrWhiteSpace(tag))
            query = query.Where(p => p.PostTags.Any(pt => pt.Tag.Slug == tag));

        var total = await query.CountAsync();

        var posts = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PostListItemViewModel
            {
                Id = p.Id,
                Title = p.Title,
                Slug = p.Slug,
                Summary = p.Summary,
                CategoryName = p.Category != null ? p.Category.Name : null,
                PublishedAtUtc = p.PublishedAtUtc,
                CoverMediaAssetId = p.CoverMediaAssetId,
                IsPublished = p.IsPublished,
                Tags = p.PostTags.Select(pt => pt.Tag.Name).ToList()
            })
            .ToListAsync();

        ViewBag.Categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
        ViewBag.CurrentCategory = category;
        ViewBag.CurrentTag = tag;
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);

        return View(posts);
    }

    public IActionResult Error() => View();
}
