using System.Text;
using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public class SeoController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly SeoService _seo;
    private readonly ISiteConfigService _site;

    public SeoController(ApplicationDbContext db, SeoService seo, ISiteConfigService site)
    {
        _db = db;
        _seo = seo;
        _site = site;
    }

    private async Task<string> ResolveBaseUrlAsync()
    {
        var configured = await _site.GetAsync(SiteSettingKeys.BaseUrl);
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.TrimEnd('/');
        return $"{Request.Scheme}://{Request.Host}";
    }

    [HttpGet("robots.txt")]
    public async Task<IActionResult> Robots()
    {
        var custom = await _site.GetAsync("RobotsTxt");
        if (!string.IsNullOrWhiteSpace(custom))
            return Content(custom.Trim() + "\n", "text/plain");

        var baseUrl = await ResolveBaseUrlAsync();
        var sb = new StringBuilder();
        sb.AppendLine("User-agent: *");
        sb.AppendLine("Allow: /");
        sb.AppendLine("Disallow: /Posts/Create");
        sb.AppendLine("Disallow: /Posts/Edit");
        sb.AppendLine("Disallow: /Account/");
        sb.AppendLine("Disallow: /Admin/");
        sb.AppendLine("Disallow: /AdminAnalytics/");
        sb.AppendLine("Disallow: /media/upload");
        sb.AppendLine();
        sb.AppendLine($"Sitemap: {baseUrl}/sitemap.xml");
        return Content(sb.ToString(), "text/plain");
    }

    [HttpGet("sitemap.xml")]
    public async Task<IActionResult> Sitemap()
    {
        var baseUrl = await ResolveBaseUrlAsync();
        var now = DateTime.UtcNow;

        var posts = await _db.Posts
            .Where(p => p.IsPublished && !p.IsDeleted)
            .Where(p => p.ExpiresAtUtc == null || p.ExpiresAtUtc > now)
            .Where(p => p.TranslationStatus == TranslationStatus.Original
                        || p.TranslationStatus == TranslationStatus.Approved)
            .Select(p => new { p.Slug, p.UpdatedAtUtc, p.LanguageCode })
            .ToListAsync();

        var categories = await _db.Categories
            .Select(c => new { c.Slug, c.Name })
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

        void Url(string loc, string? lastmod = null, string freq = "weekly", string priority = "0.7")
        {
            sb.AppendLine("  <url>");
            sb.AppendLine($"    <loc>{System.Security.SecurityElement.Escape(loc)}</loc>");
            if (!string.IsNullOrEmpty(lastmod))
                sb.AppendLine($"    <lastmod>{lastmod}</lastmod>");
            sb.AppendLine($"    <changefreq>{freq}</changefreq>");
            sb.AppendLine($"    <priority>{priority}</priority>");
            sb.AppendLine("  </url>");
        }

        Url($"{baseUrl}/", freq: "daily", priority: "1.0");

        foreach (var lang in new[] { "fa", "en", "ar" })
            Url($"{baseUrl}/{lang}/", freq: "daily", priority: "0.9");

        foreach (var post in posts)
            Url($"{baseUrl}/post/{post.Slug}", post.UpdatedAtUtc.ToString("yyyy-MM-dd"), "monthly", "0.8");

        foreach (var cat in categories)
            Url($"{baseUrl}/?category={Uri.EscapeDataString(cat.Slug)}", freq: "weekly", priority: "0.5");

        sb.AppendLine("</urlset>");
        return Content(sb.ToString(), "application/xml");
    }
}
