using System.Text;
using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services;
using BlogApp.Services.Seo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BlogApp.Controllers;

public class SeoController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly SeoService _seo;
    private readonly ISiteConfigService _site;
    private readonly IndexNowOptions _indexNow;

    public SeoController(
        ApplicationDbContext db,
        SeoService seo,
        ISiteConfigService site,
        IOptions<IndexNowOptions> indexNow)
    {
        _db = db;
        _seo = seo;
        _site = site;
        _indexNow = indexNow.Value;
    }

    private async Task<string> ResolveBaseUrlAsync()
    {
        var configured = await _site.GetAsync(SiteSettingKeys.BaseUrl);
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.TrimEnd('/');
        return $"{Request.Scheme}://{Request.Host}";
    }

    /// <summary>
    /// Paths that must never be crawled (admin, account, APIs, editors).
    /// Shared by search bots and AI bots so crawl budget is not wasted.
    /// </summary>
    private static void AppendPrivateDisallows(StringBuilder sb)
    {
        sb.AppendLine("Disallow: /Posts/Create");
        sb.AppendLine("Disallow: /Posts/Edit");
        sb.AppendLine("Disallow: /Account/");
        sb.AppendLine("Disallow: /Admin");
        sb.AppendLine("Disallow: /Admin/");
        sb.AppendLine("Disallow: /AdminAnalytics");
        sb.AppendLine("Disallow: /AdminAnalytics/");
        sb.AppendLine("Disallow: /AdminApiKeys");
        sb.AppendLine("Disallow: /AdminApiKeys/");
        sb.AppendLine("Disallow: /AdminAudit");
        sb.AppendLine("Disallow: /AdminAudit/");
        sb.AppendLine("Disallow: /AdminBackgroundJobs");
        sb.AppendLine("Disallow: /AdminBackgroundJobs/");
        sb.AppendLine("Disallow: /AdminBackup");
        sb.AppendLine("Disallow: /AdminBackup/");
        sb.AppendLine("Disallow: /AdminEnterprise");
        sb.AppendLine("Disallow: /AdminEnterprise/");
        sb.AppendLine("Disallow: /AdminModeration");
        sb.AppendLine("Disallow: /AdminModeration/");
        sb.AppendLine("Disallow: /AdminMonetization");
        sb.AppendLine("Disallow: /AdminMonetization/");
        sb.AppendLine("Disallow: /AdminNewsletter");
        sb.AppendLine("Disallow: /AdminNewsletter/");
        sb.AppendLine("Disallow: /AdminNotifications");
        sb.AppendLine("Disallow: /AdminNotifications/");
        sb.AppendLine("Disallow: /AdminReports");
        sb.AppendLine("Disallow: /AdminReports/");
        sb.AppendLine("Disallow: /AdminRoles");
        sb.AppendLine("Disallow: /AdminRoles/");
        sb.AppendLine("Disallow: /AdminSearch");
        sb.AppendLine("Disallow: /AdminSearch/");
        sb.AppendLine("Disallow: /AdminSettings");
        sb.AppendLine("Disallow: /AdminSettings/");
        sb.AppendLine("Disallow: /AdminThemes");
        sb.AppendLine("Disallow: /AdminThemes/");
        sb.AppendLine("Disallow: /AdminUsers");
        sb.AppendLine("Disallow: /AdminUsers/");
        sb.AppendLine("Disallow: /media/upload");
        sb.AppendLine("Disallow: /api/");
        sb.AppendLine("Disallow: /Identity/");
        sb.AppendLine("Disallow: /signin-");
        sb.AppendLine("Disallow: /search?");
        sb.AppendLine("Disallow: /*?*sort=");
        sb.AppendLine("Disallow: /*?*page=");
    }

    [HttpGet("robots.txt")]
    public async Task<IActionResult> Robots()
    {
        var custom = await _site.GetAsync("RobotsTxt");
        if (!string.IsNullOrWhiteSpace(custom))
            return Content(custom.Trim() + "\n", "text/plain");

        var baseUrl = await ResolveBaseUrlAsync();
        var sb = new StringBuilder();

        sb.AppendLine("# m.nazari — crawl policy (search + AI)");
        sb.AppendLine("# Private / low-value paths are disallowed for all agents.");
        sb.AppendLine("# High-value public content (posts, authors, taxonomy, pages) is allowed.");
        sb.AppendLine();
        sb.AppendLine("User-agent: *");
        sb.AppendLine("Allow: /");
        sb.AppendLine("Allow: /fa/");
        sb.AppendLine("Allow: /en/");
        sb.AppendLine("Allow: /ar/");
        sb.AppendLine("Allow: /*/post/");
        sb.AppendLine("Allow: /author/");
        AppendPrivateDisallows(sb);
        sb.AppendLine();

        foreach (var bot in new[]
                 {
                     "Googlebot", "Googlebot-Image", "Googlebot-News",
                     "Bingbot", "Slurp", "DuckDuckBot", "Yandex", "Baiduspider"
                 })
        {
            sb.AppendLine($"User-agent: {bot}");
            sb.AppendLine("Allow: /");
            AppendPrivateDisallows(sb);
            sb.AppendLine();
        }

        foreach (var bot in new[]
                 {
                     "GPTBot", "ChatGPT-User", "OAI-SearchBot",
                     "ClaudeBot", "Claude-Web", "anthropic-ai",
                     "PerplexityBot", "Perplexity-User",
                     "Google-Extended",
                     "Applebot-Extended",
                     "Bytespider",
                     "CCBot",
                     "FacebookBot", "meta-externalagent",
                     "Amazonbot",
                     "cohere-ai",
                     "Diffbot",
                     "ImagesiftBot",
                     "Omgilibot", "Omgili",
                     "YouBot",
                     "ia_archiver", "archive.org_bot"
                 })
        {
            sb.AppendLine($"User-agent: {bot}");
            sb.AppendLine("Allow: /");
            sb.AppendLine("Allow: /fa/");
            sb.AppendLine("Allow: /en/");
            sb.AppendLine("Allow: /ar/");
            sb.AppendLine("Allow: /*/post/");
            sb.AppendLine("Allow: /author/");
            AppendPrivateDisallows(sb);
            sb.AppendLine();
        }

        sb.AppendLine($"Sitemap: {baseUrl}/sitemap.xml");
        var host = baseUrl.Replace("https://", "", StringComparison.OrdinalIgnoreCase)
                          .Replace("http://", "", StringComparison.OrdinalIgnoreCase);
        sb.AppendLine($"Host: {host}");
        return Content(sb.ToString(), "text/plain; charset=utf-8");
    }

    /// <summary>IndexNow key verification file: https://host/{key}.txt</summary>
    [HttpGet("{key}.txt")]
    public IActionResult IndexNowKey(string key)
    {
        if (string.IsNullOrWhiteSpace(_indexNow.Key)
            || !string.Equals(key, _indexNow.Key, StringComparison.Ordinal))
            return NotFound();
        return Content(_indexNow.Key, "text/plain; charset=utf-8");
    }

    [HttpGet("sitemap.xml")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any, NoStore = false)]
    public async Task<IActionResult> Sitemap()
    {
        var baseUrl = await ResolveBaseUrlAsync();
        var xml = await SitemapBuilder.BuildIndexAsync(baseUrl);
        return Xml(xml);
    }

    [HttpGet("sitemap-pages.xml")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> SitemapPages()
    {
        var baseUrl = await ResolveBaseUrlAsync();
        return Xml(await SitemapBuilder.BuildPagesAsync(baseUrl));
    }

    [HttpGet("sitemap-posts.xml")]
    [ResponseCache(Duration = 1800, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> SitemapPosts()
    {
        var baseUrl = await ResolveBaseUrlAsync();
        return Xml(await SitemapBuilder.BuildPostsAsync(_db, baseUrl));
    }

    [HttpGet("sitemap-authors.xml")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> SitemapAuthors()
    {
        var baseUrl = await ResolveBaseUrlAsync();
        return Xml(await SitemapBuilder.BuildAuthorsAsync(_db, baseUrl));
    }

    [HttpGet("sitemap-taxonomies.xml")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> SitemapTaxonomies()
    {
        var baseUrl = await ResolveBaseUrlAsync();
        return Xml(await SitemapBuilder.BuildTaxonomiesAsync(_db, baseUrl));
    }

    private static ContentResult Xml(string xml) =>
        new()
        {
            Content = xml,
            ContentType = "application/xml; charset=utf-8",
            StatusCode = 200
        };
}
