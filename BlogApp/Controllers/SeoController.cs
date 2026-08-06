using System.Text;
using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services;
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

    [HttpGet("robots.txt")]
    public async Task<IActionResult> Robots()
    {
        var custom = await _site.GetAsync("RobotsTxt");
        if (!string.IsNullOrWhiteSpace(custom))
            return Content(custom.Trim() + "\n", "text/plain");

        var baseUrl = await ResolveBaseUrlAsync();
        var sb = new StringBuilder();

        // Default: invite major crawlers; block private surfaces only
        sb.AppendLine("User-agent: *");
        sb.AppendLine("Allow: /");
        sb.AppendLine("Allow: /fa/");
        sb.AppendLine("Allow: /en/");
        sb.AppendLine("Allow: /ar/");
        sb.AppendLine("Allow: /*/post/");
        sb.AppendLine("Allow: /author/");
        sb.AppendLine("Disallow: /Posts/Create");
        sb.AppendLine("Disallow: /Posts/Edit");
        sb.AppendLine("Disallow: /Account/");
        sb.AppendLine("Disallow: /Admin");
        sb.AppendLine("Disallow: /Admin/");
        sb.AppendLine("Disallow: /AdminAnalytics");
        sb.AppendLine("Disallow: /AdminAnalytics/");
        sb.AppendLine("Disallow: /media/upload");
        sb.AppendLine("Disallow: /api/");
        sb.AppendLine();

        // Explicit welcome for Google / Bing (no crawl-delay)
        foreach (var bot in new[] { "Googlebot", "Googlebot-Image", "Bingbot", "Slurp", "DuckDuckBot" })
        {
            sb.AppendLine($"User-agent: {bot}");
            sb.AppendLine("Allow: /");
            sb.AppendLine();
        }

        sb.AppendLine($"Sitemap: {baseUrl}/sitemap.xml");
        sb.AppendLine($"Host: {baseUrl.Replace("https://", "").Replace("http://", "")}");
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
    public async Task<IActionResult> Sitemap()
    {
        var baseUrl = await ResolveBaseUrlAsync();
        var now = DateTime.UtcNow;

        var posts = await _db.Posts
            .AsNoTracking()
            .Where(p => p.IsPublished && !p.IsDeleted)
            .Where(p => p.ExpiresAtUtc == null || p.ExpiresAtUtc > now)
            .Where(p => p.TranslationStatus == TranslationStatus.Original
                        || p.TranslationStatus == TranslationStatus.Approved)
            .Select(p => new
            {
                p.Id,
                p.Slug,
                p.UpdatedAtUtc,
                p.LanguageCode,
                GroupId = p.TranslationGroupId ?? p.Id
            })
            .ToListAsync();

        var byGroup = posts.GroupBy(p => p.GroupId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var categories = await _db.Categories
            .AsNoTracking()
            .Select(c => new { c.Slug, c.Name })
            .ToListAsync();

        // Authors with at least one published post
        var authors = await _db.Posts.AsNoTracking()
            .Where(p => p.IsPublished && !p.IsDeleted && p.Author != null && p.Author.UserName != null)
            .Select(p => new { p.Author!.UserName, p.UpdatedAtUtc })
            .GroupBy(x => x.UserName!)
            .Select(g => new { UserName = g.Key, LastMod = g.Max(x => x.UpdatedAtUtc) })
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\" xmlns:xhtml=\"http://www.w3.org/1999/xhtml\">");

        void Url(string loc, string? lastmod = null, string freq = "weekly", string priority = "0.7",
            IEnumerable<(string Lang, string Href)>? alternates = null)
        {
            sb.AppendLine("  <url>");
            sb.AppendLine($"    <loc>{System.Security.SecurityElement.Escape(loc)}</loc>");
            if (!string.IsNullOrEmpty(lastmod))
                sb.AppendLine($"    <lastmod>{lastmod}</lastmod>");
            sb.AppendLine($"    <changefreq>{freq}</changefreq>");
            sb.AppendLine($"    <priority>{priority}</priority>");
            if (alternates != null)
            {
                foreach (var a in alternates)
                {
                    sb.AppendLine(
                        $"    <xhtml:link rel=\"alternate\" hreflang=\"{System.Security.SecurityElement.Escape(a.Lang)}\" href=\"{System.Security.SecurityElement.Escape(a.Href)}\" />");
                }
            }
            sb.AppendLine("  </url>");
        }

        Url($"{baseUrl}/", freq: "daily", priority: "1.0");

        foreach (var lang in new[] { "fa", "en", "ar" })
            Url($"{baseUrl}/{lang}/", freq: "daily", priority: "0.9");

        // Static marketing pages
        foreach (var page in new[] { "about", "services", "projects", "contact" })
        {
            foreach (var lang in new[] { "fa", "en", "ar" })
                Url($"{baseUrl}/{lang}/pages/{page}", freq: "monthly", priority: "0.6");
        }

        foreach (var post in posts)
        {
            var loc = $"{baseUrl}/{post.LanguageCode}/post/{post.Slug}";
            List<(string, string)>? alts = null;
            if (byGroup.TryGetValue(post.GroupId, out var siblings) && siblings.Count > 1)
            {
                alts = siblings
                    .Select(s => (s.LanguageCode, $"{baseUrl}/{s.LanguageCode}/post/{s.Slug}"))
                    .ToList();
                var def = siblings.FirstOrDefault(s => s.LanguageCode == "fa") ?? siblings[0];
                alts.Add(("x-default", $"{baseUrl}/{def.LanguageCode}/post/{def.Slug}"));
            }

            Url(loc, post.UpdatedAtUtc.ToString("yyyy-MM-dd"), "monthly", "0.8", alts);
        }

        foreach (var a in authors)
            Url($"{baseUrl}/author/{Uri.EscapeDataString(a.UserName)}",
                a.LastMod.ToString("yyyy-MM-dd"), "weekly", "0.7");

        foreach (var cat in categories)
            Url($"{baseUrl}/?category={Uri.EscapeDataString(cat.Slug)}", freq: "weekly", priority: "0.5");

        sb.AppendLine("</urlset>");
        return Content(sb.ToString(), "application/xml; charset=utf-8");
    }
}
