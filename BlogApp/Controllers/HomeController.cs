using BlogApp.Data;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using BlogApp.Services.Analytics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly SeoService _seo;
    private readonly IAnalyticsTracker _analytics;

    public HomeController(ApplicationDbContext db, SeoService seo, IAnalyticsTracker analytics)
    {
        _db = db;
        _seo = seo;
        _analytics = analytics;
    }

    public async Task<IActionResult> Index(string? category, string? tag, string? q, int page = 1)
    {
        const int pageSize = 8;
        var isAuthor = User.Identity?.IsAuthenticated == true;
        var now = DateTime.UtcNow;

        var query = _db.Posts
            .Where(p => !p.IsDeleted)
            .Where(p => p.IsPublished
                        || isAuthor
                        || (p.ScheduledPublishAtUtc != null && p.ScheduledPublishAtUtc <= now))
            .Where(p => p.ExpiresAtUtc == null || p.ExpiresAtUtc > now || isAuthor)
            .Include(p => p.Category)
            .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
            .OrderByDescending(p => p.IsSticky)
            .ThenByDescending(p => p.IsFeatured)
            .ThenByDescending(p => p.IsPublished ? p.PublishedAtUtc : p.CreatedAtUtc)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category != null && p.Category.Slug == category);

        if (!string.IsNullOrWhiteSpace(tag))
            query = query.Where(p => p.PostTags.Any(pt => pt.Tag.Slug == tag));

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            if (term.Length > 100) term = term[..100];
            query = query.Where(p =>
                p.Title.Contains(term) ||
                (p.Summary != null && p.Summary.Contains(term)) ||
                p.ContentMarkdown.Contains(term));
        }

        var total = await query.CountAsync();

        if (!string.IsNullOrWhiteSpace(q))
            await _analytics.TrackSearchAsync(HttpContext, q.Trim(), total);

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
                IsFeatured = p.IsFeatured,
                IsSticky = p.IsSticky,
                ReadingTimeMinutes = p.ReadingTimeMinutes,
                Tags = p.PostTags.Select(pt => pt.Tag.Name).ToList()
            })
            .ToListAsync();

        ViewBag.Categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
        ViewBag.CurrentCategory = category;
        ViewBag.CurrentTag = tag;
        ViewBag.SearchQuery = q;
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);

        ViewData["Description"] = _seo.SiteDescription;
        ViewData["OgType"] = "website";
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        ViewData["Canonical"] = string.IsNullOrEmpty(category) && string.IsNullOrEmpty(tag) && string.IsNullOrEmpty(q) && page == 1
            ? baseUrl + "/"
            : $"{baseUrl}{Request.Path}{Request.QueryString}";
        ViewBag.WebsiteJsonLd = _seo.BuildWebsiteJsonLd(baseUrl);

        return View(posts);
    }

    public IActionResult Error() => View();
}
