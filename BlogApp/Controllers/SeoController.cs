using System.Text;
using BlogApp.Data;
using BlogApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

/// <summary>Serves /robots.txt and /sitemap.xml — kept dynamic so the sitemap always
/// reflects currently published posts without a separate build step.</summary>
public class SeoController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly SeoService _seo;
    private readonly IConfiguration _config;

    public SeoController(ApplicationDbContext db, SeoService seo, IConfiguration config)
    {
        _db = db;
        _seo = seo;
        _config = config;
    }

    private string BaseUrl => $"{Request.Scheme}://{Request.Host}";

    [HttpGet("robots.txt")]
    public IActionResult Robots()
    {
        var sb = new StringBuilder();
        sb.AppendLine("User-agent: *");
        sb.AppendLine("Allow: /");
        sb.AppendLine("Disallow: /Posts/Create");
        sb.AppendLine("Disallow: /Posts/Edit");
        sb.AppendLine("Disallow: /Account/");
        sb.AppendLine("Disallow: /Admin/");
        sb.AppendLine("Disallow: /media/upload");
        sb.AppendLine();
        sb.AppendLine($"Sitemap: {BaseUrl}/sitemap.xml");
        return Content(sb.ToString(), "text/plain");
    }

    [HttpGet("sitemap.xml")]
    public async Task<IActionResult> Sitemap()
    {
        var posts = await _db.Posts
            .Where(p => p.IsPublished)
            .Select(p => new { p.Slug, p.UpdatedAtUtc })
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

        sb.AppendLine("  <url>");
        sb.AppendLine($"    <loc>{BaseUrl}/</loc>");
        sb.AppendLine("    <changefreq>daily</changefreq>");
        sb.AppendLine("    <priority>1.0</priority>");
        sb.AppendLine("  </url>");

        foreach (var post in posts)
        {
            sb.AppendLine("  <url>");
            sb.AppendLine($"    <loc>{BaseUrl}/post/{post.Slug}</loc>");
            sb.AppendLine($"    <lastmod>{post.UpdatedAtUtc:yyyy-MM-dd}</lastmod>");
            sb.AppendLine("    <changefreq>monthly</changefreq>");
            sb.AppendLine("    <priority>0.8</priority>");
            sb.AppendLine("  </url>");
        }

        sb.AppendLine("</urlset>");
        return Content(sb.ToString(), "application/xml");
    }
}
