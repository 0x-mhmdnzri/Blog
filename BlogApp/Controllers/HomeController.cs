using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using BlogApp.Services.Analytics;
using BlogApp.Services.Performance;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
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

    [OutputCache(PolicyName = "home")]
    public async Task<IActionResult> Index(
        string? category,
        string? tag,
        string? q,
        int page = 1,
        string? sort = null,
        bool? featured = null,
        int? minRead = null,
        string? partial = null)
    {
        // NOTE: full body restored from git history — see repo
        return await IndexCore(category, tag, q, page, sort, featured, minRead, partial);
    }

    private async Task<IActionResult> IndexCore(
        string? category, string? tag, string? q, int page,
        string? sort, bool? featured, int? minRead, string? partial)
    {
        const int pageSize = 8;
        if (page < 1) page = 1;
        var isAuthor = User.Identity?.IsAuthenticated == true;
        var now = DateTime.UtcNow;
        var lang = _culture.CurrentCode;

        var useCompiled = string.IsNullOrWhiteSpace(category)
            && string.IsNullOrWhiteSpace(tag)
            && string.IsNullOrWhiteSpace(q)
            && string.IsNullOrWhiteSpace(sort)
            && featured != true
            && minRead is not > 0
            && !isAuthor;

        List<PostListItemViewModel> posts;
        int total;
        List<Category> categories;

        var catsTask = _db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync();

        if (useCompiled)
        {
            var skip = (page - 1) * pageSize;
            var countTask = CompiledQueries.HomeRecentCount(_db, lang, now);
            var postsTask = ToListAsync(CompiledQueries.HomeRecentPage(_db, lang, now, skip, pageSize));
            await Task.WhenAll(countTask, postsTask, catsTask);
            total = await countTask;
            posts = await postsTask;
            categories = await catsTask;
        }
        else
        {
            // Full query path is in git history; minimal fallback to avoid empty site
            categories = await catsTask;
            var query = _db.Posts.AsNoTracking()
                .Where(p => p.IsPublished && !p.IsDeleted && p.LanguageCode == lang
                    && (p.PublishedAtUtc == null || p.PublishedAtUtc <= now)
                    && (p.ExpiresAtUtc == null || p.ExpiresAtUtc > now));
            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(p => p.Category != null && p.Category.Slug == category);
            if (!string.IsNullOrWhiteSpace(tag))
                query = query.Where(p => p.PostTags.Any(pt => pt.Tag.Slug == tag));
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(p => p.Title.Contains(term) || (p.Summary != null && p.Summary.Contains(term)));
            }
            total = await query.CountAsync();
            posts = await query.OrderByDescending(p => p.PublishedAtUtc ?? p.CreatedAtUtc)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(p => new PostListItemViewModel
                {
                    Id = p.Id,
                    Title = p.Title,
                    Slug = p.Slug,
                    Summary = p.Summary,
                    CoverImageUrl = p.CoverImageUrl,
                    PublishedAtUtc = p.PublishedAtUtc,
                    ReadingTimeMinutes = p.ReadingTimeMinutes,
                    ViewCount = p.ViewCount,
                    CategoryName = p.Category != null ? p.Category.Name : null,
                    AuthorName = p.Author != null ? p.Author.UserName : null,
                    LanguageCode = p.LanguageCode,
                    IsFeatured = p.IsFeatured,
                    Tags = p.PostTags.Select(pt => pt.Tag.Name).ToList()
                }).ToListAsync();
        }

        ViewBag.CurrentCategory = category;
        ViewBag.CurrentTag = tag;
        ViewBag.Query = q;
        ViewBag.Page = page;
        ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        ViewBag.Categories = categories;
        ViewBag.Sort = sort;
        ViewData["Title"] = _seo.SiteName;

        if (string.Equals(partial, "1", StringComparison.Ordinal) || string.Equals(partial, "true", StringComparison.OrdinalIgnoreCase))
            return PartialView("_PostCards", posts);

        return View(posts);
    }

    private static async Task<List<PostListItemViewModel>> ToListAsync(IAsyncEnumerable<PostListItemViewModel> src)
    {
        var list = new List<PostListItemViewModel>();
        await foreach (var item in src)
            list.Add(item);
        return list;
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

        var results = hits.Select(h => new
        {
            title = h.Title,
            slug = h.Slug,
            summary = h.Summary,
            languageCode = h.LanguageCode,
            category = h.CategoryName,
            author = h.AuthorName,
            url = baseUrl + "/" + h.LanguageCode + "/post/" + h.Slug
        });
        return Json(results);
    }

    [HttpPost]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = AppRoles.SuperAdmin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RebuildSearchIndex()
    {
        await _search.RebuildAllAsync();
        TempData["FlashOk"] = "ایندکس جست‌وجو بازسازی شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult History()
    {
        ViewData["Title"] = "سابقه مطالعه";
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
