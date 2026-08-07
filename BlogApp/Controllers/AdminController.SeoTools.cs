using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using BlogApp.Services.Seo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BlogApp.Controllers;

public partial class AdminController
{
    [HttpGet]
    public async Task<IActionResult> SeoTools(string? tab = null, int days = 30)
    {
        var site = HttpContext.RequestServices.GetRequiredService<ISiteConfigService>();
        var indexOpt = HttpContext.RequestServices.GetRequiredService<IOptions<IndexNowOptions>>().Value;

        var redirects = await _db.RedirectRules
            .OrderByDescending(r => r.IsActive)
            .ThenByDescending(r => r.HitCount)
            .ThenByDescending(r => r.CreatedAtUtc)
            .Take(200)
            .ToListAsync();

        var broken = await _db.BrokenLinkReports
            .OrderByDescending(b => b.DetectedAtUtc)
            .Take(200)
            .ToListAsync();

        var posts = await _db.Posts
            .Where(p => p.IsPublished && !p.IsDeleted)
            .Select(p => new { p.Id, p.Title, p.Slug, p.Summary, p.CoverMediaAssetId })
            .ToListAsync();

        var health = posts.Select(p =>
        {
            var hasSum = !string.IsNullOrWhiteSpace(p.Summary);
            var hasCover = p.CoverMediaAssetId is > 0;
            var score = 40 + (hasSum ? 30 : 0) + (hasCover ? 30 : 0);
            return new SeoPostHealthItem
            {
                Id = p.Id,
                Title = p.Title,
                Slug = p.Slug,
                HasSummary = hasSum,
                HasCover = hasCover,
                Score = score
            };
        }).OrderBy(h => h.Score).ThenBy(h => h.Title).Take(40).ToList();

        var baseUrl = (await site.GetAsync(SiteSettingKeys.BaseUrl) ?? $"{Request.Scheme}://{Request.Host}").TrimEnd('/');

        var vm = new SeoToolsViewModel
        {
            Meta = new SeoMetaForm
            {
                SiteName = await site.GetAsync(SiteSettingKeys.SiteName) ?? "",
                SiteDescription = await site.GetAsync(SiteSettingKeys.SiteDescription) ?? "",
                AuthorName = await site.GetAsync(SiteSettingKeys.AuthorName) ?? "",
                TwitterHandle = await site.GetAsync(SiteSettingKeys.TwitterHandle) ?? "",
                BaseUrl = await site.GetAsync(SiteSettingKeys.BaseUrl) ?? "",
                RobotsTxt = await site.GetAsync("RobotsTxt")
            },
            Redirects = redirects,
            BrokenLinks = broken,
            PostHealth = health,
            PublishedCount = posts.Count,
            MissingSummaryCount = posts.Count(p => string.IsNullOrWhiteSpace(p.Summary)),
            MissingCoverCount = posts.Count(p => p.CoverMediaAssetId is null or 0),
            SitemapUrl = "/sitemap.xml",
            RobotsUrl = "/robots.txt",
            ActiveTab = tab ?? "overview",
            IndexNowEnabled = indexOpt.Enabled,
            IndexNowHasKey = !string.IsNullOrWhiteSpace(indexOpt.Key),
            IndexNowKeyHint = string.IsNullOrWhiteSpace(indexOpt.Key)
                ? null
                : (indexOpt.Key.Length <= 8 ? indexOpt.Key : indexOpt.Key[..4] + "…" + indexOpt.Key[^4..]),
            IndexNowKeyUrl = string.IsNullOrWhiteSpace(indexOpt.Key) ? null : $"{baseUrl}/{indexOpt.Key}.txt"
        };

        ViewBag.BaseUrlPreview = baseUrl;

        if (string.Equals(vm.ActiveTab, "crawl", StringComparison.OrdinalIgnoreCase)
            || string.Equals(vm.ActiveTab, "overview", StringComparison.OrdinalIgnoreCase)
            || string.Equals(vm.ActiveTab, "redirects", StringComparison.OrdinalIgnoreCase))
        {
            vm.Crawl = await BotCrawlSummary.BuildAsync(_db, days);
            ViewBag.CrawlWaste = await CrawlWasteAnalyzer.BuildAsync(_db, days);
        }

        return View("SeoTools", vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> SeoSaveMeta(SeoMetaForm model)
    {
        var site = HttpContext.RequestServices.GetRequiredService<ISiteConfigService>();
        await site.SetAsync(SiteSettingKeys.SiteName, model.SiteName?.Trim());
        await site.SetAsync(SiteSettingKeys.SiteDescription, model.SiteDescription?.Trim());
        await site.SetAsync(SiteSettingKeys.AuthorName, model.AuthorName?.Trim());
        await site.SetAsync(SiteSettingKeys.TwitterHandle, model.TwitterHandle?.Trim()?.TrimStart('@'));
        await site.SetAsync(SiteSettingKeys.BaseUrl, model.BaseUrl?.Trim()?.TrimEnd('/'));
        await site.SetAsync("RobotsTxt", string.IsNullOrWhiteSpace(model.RobotsTxt) ? null : model.RobotsTxt.Trim());

        TempData["SeoOk"] = _t["seo.saved_meta"];
        return RedirectToAction(nameof(SeoTools), new { tab = "meta" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SeoAddRedirect(RedirectForm model)
    {
        if (!ModelState.IsValid)
        {
            TempData["SeoErr"] = _t["seo.err_redirect_form"];
            return RedirectToAction(nameof(SeoTools), new { tab = "redirects" });
        }

        var from = NormalizeFromPath(model.FromPath);
        if (string.IsNullOrEmpty(from))
        {
            TempData["SeoErr"] = _t["seo.err_from_path"];
            return RedirectToAction(nameof(SeoTools), new { tab = "redirects" });
        }

        var to = model.ToUrl.Trim();
        if (string.IsNullOrEmpty(to))
        {
            TempData["SeoErr"] = _t["seo.err_to_url"];
            return RedirectToAction(nameof(SeoTools), new { tab = "redirects" });
        }

        var status = model.StatusCode is 301 or 302 or 307 or 308 ? model.StatusCode : 301;

        var existing = await _db.RedirectRules.FirstOrDefaultAsync(r => r.FromPath == from);
        if (existing is not null)
        {
            existing.ToUrl = to;
            existing.StatusCode = status;
            existing.Notes = model.Notes?.Trim();
            existing.IsActive = true;
        }
        else
        {
            _db.RedirectRules.Add(new RedirectRule
            {
                FromPath = from,
                ToUrl = to,
                StatusCode = status,
                Notes = model.Notes?.Trim(),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        TempData["SeoOk"] = _t["seo.saved_redirect"];
        return RedirectToAction(nameof(SeoTools), new { tab = "redirects" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SeoToggleRedirect(int id)
    {
        var rule = await _db.RedirectRules.FindAsync(id);
        if (rule is not null)
        {
            rule.IsActive = !rule.IsActive;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(SeoTools), new { tab = "redirects" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SeoDeleteRedirect(int id)
    {
        var rule = await _db.RedirectRules.FindAsync(id);
        if (rule is not null)
        {
            _db.RedirectRules.Remove(rule);
            await _db.SaveChangesAsync();
            TempData["SeoOk"] = _t["seo.deleted_redirect"];
        }
        return RedirectToAction(nameof(SeoTools), new { tab = "redirects" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SeoScanBrokenLinks()
    {
        var scanner = HttpContext.RequestServices.GetRequiredService<BrokenLinkService>();
        var count = await scanner.ScanAsync();
        TempData["SeoOk"] = string.Format(_t["seo.scan_done"], count);
        return RedirectToAction(nameof(SeoTools), new { tab = "broken" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> SeoImport(
        IFormFile? file,
        string format = "wordpress",
        string languageCode = "fa",
        bool createRedirects = true,
        bool publishImmediately = true)
    {
        if (file is null || file.Length == 0)
        {
            TempData["SeoErr"] = _t["seo.import_no_file"];
            return RedirectToAction(nameof(SeoTools), new { tab = "import" });
        }

        if (file.Length > 50 * 1024 * 1024)
        {
            TempData["SeoErr"] = _t["seo.import_too_large"];
            return RedirectToAction(nameof(SeoTools), new { tab = "import" });
        }

        var importer = HttpContext.RequestServices.GetRequiredService<IMigrationImportService>();
        var authorId = AuthorAccess.UserId(User)!;

        await using var stream = file.OpenReadStream();
        MigrationImportResult result;
        try
        {
            if (string.Equals(format, "ghost", StringComparison.OrdinalIgnoreCase))
            {
                result = await importer.ImportGhostJsonAsync(
                    stream, authorId, languageCode, createRedirects, publishImmediately);
            }
            else
            {
                result = await importer.ImportWordPressWxrAsync(
                    stream, authorId, languageCode, createRedirects, publishImmediately);
            }
        }
        catch (Exception ex)
        {
            TempData["SeoErr"] = string.Format(_t["seo.import_failed"], ex.Message);
            return RedirectToAction(nameof(SeoTools), new { tab = "import" });
        }

        var msg = string.Format(_t["seo.import_done"],
            result.PostsCreated, result.PostsSkipped, result.RedirectsCreated);
        if (result.Warnings.Count > 0)
            msg += " · " + string.Join("; ", result.Warnings.Take(5));
        TempData["SeoOk"] = msg;
        return RedirectToAction(nameof(SeoTools), new { tab = "import" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> SeoIndexNowSubmitAll()
    {
        var indexNow = HttpContext.RequestServices.GetRequiredService<IIndexNowService>();
        try
        {
            var count = await indexNow.SubmitAllPublishedAsync();
            TempData["SeoOk"] = string.Format(_t["seo.indexnow_done"], count);
        }
        catch (Exception ex)
        {
            TempData["SeoErr"] = string.Format(_t["seo.indexnow_failed"], ex.Message);
        }
        return RedirectToAction(nameof(SeoTools), new { tab = "indexnow" });
    }

    private static string NormalizeFromPath(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var p = raw.Trim();
        if (Uri.TryCreate(p, UriKind.Absolute, out var abs))
            p = abs.AbsolutePath;
        if (!p.StartsWith('/')) p = "/" + p;
        var q = p.IndexOf('?');
        if (q >= 0) p = p[..q];
        var h = p.IndexOf('#');
        if (h >= 0) p = p[..h];
        while (p.Length > 1 && p.EndsWith('/')) p = p[..^1];
        return p;
    }
}
