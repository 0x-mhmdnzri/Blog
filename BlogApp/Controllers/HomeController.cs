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
        string? partial = null)
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
            List<int>? ftsOrder = null;
            var query = _db.Posts
                .AsNoTracking()
                .Where(p => !p.IsDeleted)
                .Where(p => p.LanguageCode == lang)
                .Where(p => p.IsPublished
                            || isAuthor
                            || (p.ScheduledPublishAtUtc != null && p.ScheduledPublishAtUtc <= now))
                .Where(p => p.ExpiresAtUtc == null || p.ExpiresAtUtc > now || isAuthor)
                .Where(p => isAuthor
                            || p.TranslationStatus == TranslationStatus.Original
                            || p.TranslationStatus == TranslationStatus.Approved);

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(p => p.Category != null && p.Category.Slug == category);

            if (!string.IsNullOrWhiteSpace(tag))
                query = query.Where(p => p.PostTags.Any(pt => pt.Tag.Slug == tag));

            if (featured == true)
                query = query.Where(p => p.IsFeatured);
            if (minRead is > 0)
                query = query.Where(p => p.ReadingTimeMinutes >= minRead);

            if (!string.IsNullOrWhiteSpace(q))
            {
                ftsOrder = await _search.SearchPostIdsAsync(q, take: 500);
                var ftsIds = ftsOrder;
                if (ftsIds.Count == 0)
                    query = query.Where(p => false);
                else
                {
                    query = _db.Posts
                        .AsNoTracking()
                        .Where(p => !p.IsDeleted)
                        .Where(p => p.IsPublished
                                    || isAuthor
                                    || (p.ScheduledPublishAtUtc != null && p.ScheduledPublishAtUtc <= now))
                        .Where(p => p.ExpiresAtUtc == null || p.ExpiresAtUtc > now || isAuthor)
                        .Where(p => isAuthor
                                    || p.TranslationStatus == TranslationStatus.Original
                                    || p.TranslationStatus == TranslationStatus.Approved)
                        .Where(p => ftsIds.Contains(p.Id));

                    if (!string.IsNullOrWhiteSpace(category))
                        query = query.Where(p => p.Category != null && p.Category.Slug == category);
                    if (!string.IsNullOrWhiteSpace(tag))
                        query = query.Where(p => p.PostTags.Any(pt => pt.Tag.Slug == tag));
                    if (featured == true)
                        query = query.Where(p => p.IsFeatured);
                    if (minRead is > 0)
                        query = query.Where(p => p.ReadingTimeMinutes >= minRead);
                }
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

            var totalTask = projected.CountAsync();
            var postsTask = projected.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            await Task.WhenAll(totalTask, postsTask, catsTask);

            total = await totalTask;
            posts = await postsTask;
            categories = await catsTask;

            if (ftsOrder is { Count: > 0 } && string.IsNullOrWhiteSpace(sort))
            {
                var rank = ftsOrder.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
                posts = posts.OrderBy(p => rank.GetValueOrDefault(p.Id, int.MaxValue)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(q))
                await _analytics.TrackSearchAsync(HttpContext, q.Trim(), total);
        }

        ViewBag.Categories = categories;
        ViewBag.CurrentCategory = category;
        ViewBag.CurrentTag = tag;
        ViewBag.SearchQuery = q;
        ViewBag.Page = page;
        ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        ViewBag.TotalCount = total;
        ViewBag.CurrentCulture = _culture.Current;
        ViewBag.Sort = sort;
        ViewBag.Featured = featured;
        ViewBag.MinRead = minRead;

        if (string.Equals(partial, "1", StringComparison.Ordinal)
            || (string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase) && page > 1))
        {
            return PartialView("_PostCards", posts);
        }

        ViewData["Description"] = _seo.SiteDescription;
        ViewData["OgType"] = "website";
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var canonical = string.IsNullOrEmpty(category) && string.IsNullOrEmpty(tag) && string.IsNullOrEmpty(q) && page == 1
            ? $"{baseUrl}/{lang}/"
            : $"{baseUrl}/{lang}{Request.Path}{Request.QueryString}";
        ViewData["Canonical"] = canonical;
        ViewBag.WebsiteJsonLd = _seo.BuildWebsiteJsonLd(baseUrl);
        ViewBag.CollectionJsonLd = _seo.BuildCollectionJsonLd(
            baseUrl,
            canonical,
            ViewData["Title"] as string ?? _seo.SiteName,
            _seo.SiteDescription,
            posts.Select(p => (
                p.Title,
                $"{baseUrl}/{p.LanguageCode}/post/{p.Slug}",
                p.PublishedAtUtc?.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'")
            )));

        return View(posts);
    }

    private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source)
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

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? statusCode = null)
    {
        var feature = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
        var code = statusCode ?? HttpContext.Response.StatusCode;
        if (code < 400)
            code = 500;

        HttpContext.Response.StatusCode = code;

        var known = code is 400 or 401 or 403 or 404 or 405 or 408 or 429 or 500 or 502 or 503
            ? code
            : 0;

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
