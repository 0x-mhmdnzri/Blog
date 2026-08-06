using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using BlogApp.Services.Analytics;
using BlogApp.Services.Performance;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly SeoService _seo;
    private readonly IAnalyticsTracker _analytics;
    private readonly ICultureService _culture;
    private readonly IUiTranslator _t;
    private readonly SearchIndexService _search;

    public HomeController(
        ApplicationDbContext db,
        SeoService seo,
        IAnalyticsTracker analytics,
        ICultureService culture,
        IUiTranslator t,
        SearchIndexService search)
    {
        _db = db;
        _seo = seo;
        _analytics = analytics;
        _culture = culture;
        _t = t;
        _search = search;
    }

    public async Task<IActionResult> Index(
        string? category,
        string? tag,
        string? q,
        int page = 1,
        string? sort = null,
        bool? featured = null,
        int? minRead = null,
        int? folderId = null,
        string? partial = null)
    {
        const int pageSize = 8;
        if (page < 1) page = 1;
        var now = DateTime.UtcNow;
        var lang = _culture.CurrentCode;
        var userId = AuthorAccess.UserId(User);
        var canSeeAllDrafts = AuthorAccess.CanManageAllPosts(User);

        List<PostListItemViewModel> posts;
        int total;
        var categories = await _db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync();

        var query = _db.Posts
            .AsNoTracking()
            .Where(p => !p.IsDeleted)
            .Where(p => p.LanguageCode == lang)
            .Where(p => p.IsPublished
                        || (canSeeAllDrafts && !p.IsPublished)
                        || (!p.IsPublished && userId != null && p.AuthorId == userId))
            .Where(p => p.IsPublished
                        ? (p.ExpiresAtUtc == null || p.ExpiresAtUtc > now)
                        : true)
            .Where(p => p.IsPublished
                        ? (p.TranslationStatus == TranslationStatus.Original
                           || p.TranslationStatus == TranslationStatus.Approved)
                        : true);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category != null && p.Category.Slug == category);
        if (!string.IsNullOrWhiteSpace(tag))
            query = query.Where(p => p.PostTags.Any(pt => pt.Tag.Slug == tag));
        if (folderId is int fid && fid > 0)
            query = query.Where(p => _db.Set<PostFolderItem>().Any(i => i.FolderId == fid && i.PostId == p.Id));
        else if (string.IsNullOrWhiteSpace(q) && string.IsNullOrWhiteSpace(category) && string.IsNullOrWhiteSpace(tag))
        {
            query = query.Where(p => !_db.Set<PostFolderItem>().Any(i => i.PostId == p.Id));
        }
        if (featured == true)
            query = query.Where(p => p.IsFeatured);
        if (minRead is > 0)
            query = query.Where(p => p.ReadingTimeMinutes >= minRead);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var ftsIds = await _search.SearchPostIdsAsync(q, take: 500);
            if (ftsIds.Count == 0)
                query = query.Where(p => false);
            else
                query = query.Where(p => ftsIds.Contains(p.Id));
        }

        query = (sort?.ToLowerInvariant()) switch
        {
            "popular" => query.OrderByDescending(p => p.ViewCount).ThenByDescending(p => p.PublishedAtUtc),
            "oldest" => query.OrderBy(p => p.PublishedAtUtc ?? p.CreatedAtUtc),
            "read" => query.OrderBy(p => p.ReadingTimeMinutes).ThenByDescending(p => p.PublishedAtUtc),
            _ => query.OrderByDescending(p => p.IsSticky)
                      .ThenByDescending(p => p.IsFeatured)
                      .ThenByDescending(p => p.IsPublished ? p.PublishedAtUtc : p.CreatedAtUtc)
        };

        var projected = query.Select(p => new PostListItemViewModel
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
            LanguageCode = p.LanguageCode,
            Tags = p.PostTags.Select(pt => pt.Tag.Name).ToList()
        });

        total = await projected.CountAsync();
        posts = await projected.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        if (!string.IsNullOrWhiteSpace(q))
            try { await _analytics.TrackSearchAsync(HttpContext, q.Trim(), total); } catch { }

        var folders = await _db.Set<PostFolder>().AsNoTracking()
            .Where(f => f.Items.Any(i => i.Post.IsPublished && !i.Post.IsDeleted && i.Post.LanguageCode == lang))
            .OrderBy(f => f.DisplayOrder).ThenBy(f => f.Name)
            .Select(f => new BlogFeedFolderItem
            {
                Id = f.Id,
                Name = f.Name,
                Color = f.Color,
                Count = f.Items.Count(i => i.Post.IsPublished && !i.Post.IsDeleted && i.Post.LanguageCode == lang)
            })
            .Where(f => f.Count > 0)
            .ToListAsync();

        ViewBag.Folders = folders;
        ViewBag.CurrentFolderId = folderId;
        if (folderId is int openId && openId > 0)
        {
            var openFolder = folders.FirstOrDefault(f => f.Id == openId)
                ?? await _db.Set<PostFolder>().AsNoTracking()
                    .Where(f => f.Id == openId)
                    .Select(f => new BlogFeedFolderItem { Id = f.Id, Name = f.Name, Color = f.Color, Count = f.Items.Count })
                    .FirstOrDefaultAsync();
            ViewBag.CurrentFolder = openFolder;
        }
        ViewBag.Categories = categories;
        ViewBag.CurrentCategory = category;
        ViewBag.CurrentTag = tag;
        ViewBag.SearchQuery = q;
        ViewBag.Page = page;
        ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        ViewBag.TotalCount = total;
        ViewBag.Sort = sort;
        ViewBag.Featured = featured;
        ViewBag.MinRead = minRead;

        if (string.Equals(partial, "1", StringComparison.Ordinal)
            || (string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase) && page > 1))
            return PartialView("_PostCards", posts);

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var homeUrl = $"{baseUrl}/{lang}/";
        var isFiltered = !string.IsNullOrWhiteSpace(q)
                         || !string.IsNullOrWhiteSpace(category)
                         || !string.IsNullOrWhiteSpace(tag)
                         || folderId is > 0
                         || featured == true
                         || page > 1;

        ViewData["OgType"] = "website";
        ViewData["OgImage"] = $"{baseUrl}/og/site.png";
        ViewData["OgImageAlt"] = _seo.SiteName;
        ViewData["Canonical"] = isFiltered ? $"{baseUrl}{Request.Path}{Request.QueryString}" : homeUrl;
        ViewData["Description"] = !string.IsNullOrWhiteSpace(q)
            ? $"Search results for “{q.Trim()}” · {_seo.SiteName}"
            : !string.IsNullOrWhiteSpace(category)
                ? $"{category} — {_seo.SiteDescription}"
                : !string.IsNullOrWhiteSpace(tag)
                    ? $"#{tag} — {_seo.SiteDescription}"
                    : _seo.SiteDescription;

        if (!isFiltered)
        {
            ViewData["Keywords"] = string.Join(", ",
                new[] { _seo.SiteName, _seo.AuthorName, "blog", "articles", lang }
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Concat(categories.Take(8).Select(c => c.Name)));
            ViewBag.WebsiteJsonLd = _seo.BuildWebsiteJsonLd(baseUrl);
        }
        else if (page > 3)
        {
            ViewData["NoIndex"] = true;
        }

        return View(posts);
    }

    [HttpGet]
    [ResponseCache(Duration = 10, VaryByQueryKeys = new[] { "q" }, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> SearchSuggest(string? q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 1)
            return Json(Array.Empty<object>());

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var hits = await _search.SearchHitsAsync(q, take: 10);
        try { await _analytics.TrackSearchAsync(HttpContext, q.Trim(), hits.Count); } catch { }

        return Json(hits.Select(h => new
        {
            title = h.Title,
            slug = h.Slug,
            summary = h.Summary,
            languageCode = h.LanguageCode,
            category = h.CategoryName,
            author = h.AuthorName,
            url = baseUrl + "/" + h.LanguageCode + "/post/" + h.Slug
        }));
    }

    [HttpPost]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = AppRoles.SuperAdmin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RebuildSearchIndex()
    {
        await _search.RebuildAllAsync();
        TempData["FlashOk"] = "Search index rebuilt.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult History()
    {
        ViewData["Title"] = "History";
        ViewData["NoIndex"] = true;
        return View();
    }

    [AcceptVerbs("GET", "POST", "HEAD")]
    [IgnoreAntiforgeryToken]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? statusCode = null)
    {
        var feature = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
        var code = statusCode ?? HttpContext.Response.StatusCode;
        if (code < 400) code = 500;
        HttpContext.Response.StatusCode = code;
        var known = code is 400 or 401 or 403 or 404 or 405 or 408 or 429 or 500 or 502 or 503 ? code : 0;
        var titleKey = known > 0 ? $"err.{known}.title" : "err.generic.title";
        var msgKey = known > 0 ? $"err.{known}.msg" : "err.generic.msg";
        ViewData["Title"] = _t[titleKey];
        ViewData["NoIndex"] = true;
        ViewBag.StatusCode = code;
        ViewBag.ErrorTitle = _t[titleKey];
        ViewBag.ErrorMessage = _t[msgKey];
        ViewBag.OriginalPath = feature?.OriginalPath;
        return View();
    }
}

/// <summary>Lightweight folder chip for the public blog feed.</summary>
public sealed class BlogFeedFolderItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "blue";
    public int Count { get; set; }
}
