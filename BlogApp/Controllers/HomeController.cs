using BlogApp.Data;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly SeoService _seo;

    public HomeController(ApplicationDbContext db, SeoService seo)
    {
        _db = db;
        _seo = seo;
    }

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

        ViewData["Description"] = _seo.SiteDescription;
        ViewData["OgType"] = "website";
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        ViewData["Canonical"] = string.IsNullOrEmpty(category) && string.IsNullOrEmpty(tag) && page == 1
            ? baseUrl + "/"
            : $"{baseUrl}{Request.Path}{Request.QueryString}";
        ViewBag.WebsiteJsonLd = _seo.BuildWebsiteJsonLd(baseUrl);

        return View(posts);
    }

    public IActionResult Error() => View();
}
