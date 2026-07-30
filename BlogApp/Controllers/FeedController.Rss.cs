using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;
using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public partial class FeedController : Controller
{
    // Activity feed may already exist — RSS/Atom live here as additional actions if controller exists.
}

[Route("feed")]
public class PublicFeedController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly SeoService _seo;
    private readonly ICultureService _culture;

    public PublicFeedController(ApplicationDbContext db, SeoService seo, ICultureService culture)
    {
        _db = db;
        _seo = seo;
        _culture = culture;
    }

    [HttpGet("rss")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    [OutputCache(PolicyName = "public-page")]
    public async Task<IActionResult> Rss([FromQuery] string? lang = null)
    {
        var feed = await BuildFeedAsync(lang);
        return File(Serialize(feed, "rss"), "application/rss+xml; charset=utf-8");
    }

    [HttpGet("atom")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    [OutputCache(PolicyName = "public-page")]
    public async Task<IActionResult> Atom([FromQuery] string? lang = null)
    {
        var feed = await BuildFeedAsync(lang);
        return File(Serialize(feed, "atom"), "application/atom+xml; charset=utf-8");
    }

    private async Task<SyndicationFeed> BuildFeedAsync(string? lang)
    {
        lang = string.IsNullOrWhiteSpace(lang) ? _culture.CurrentCode : lang.Trim();
        if (lang.Length > 8) lang = lang[..8];

        var baseUrl = string.IsNullOrWhiteSpace(_seo.BaseUrl)
            ? $"{Request.Scheme}://{Request.Host}"
            : _seo.BaseUrl.TrimEnd('/');

        var posts = await _db.Posts.AsNoTracking()
            .Where(p => !p.IsDeleted && p.IsPublished && p.LanguageCode == lang)
            .Where(p => p.TranslationStatus == TranslationStatus.Original
                        || p.TranslationStatus == TranslationStatus.Approved)
            .OrderByDescending(p => p.PublishedAtUtc)
            .Take(50)
            .Select(p => new { p.Title, p.Slug, p.Summary, p.PublishedAtUtc, p.LanguageCode, p.AuthorId })
            .ToListAsync();

        var items = posts.Select(p =>
        {
            var url = $"{baseUrl}/{p.LanguageCode}/post/{p.Slug}";
            var item = new SyndicationItem(
                p.Title,
                p.Summary ?? "",
                new Uri(url),
                url,
                p.PublishedAtUtc ?? DateTime.UtcNow);
            item.PublishDate = p.PublishedAtUtc ?? DateTime.UtcNow;
            return item;
        }).ToList();

        var feed = new SyndicationFeed(
            _seo.SiteName,
            _seo.SiteDescription,
            new Uri(baseUrl + "/"),
            baseUrl + "/feed/rss",
            DateTimeOffset.UtcNow)
        {
            Items = items,
            Language = lang
        };
        return feed;
    }

    private static byte[] Serialize(SyndicationFeed feed, string format)
    {
        using var ms = new MemoryStream();
        var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true };
        using (var writer = XmlWriter.Create(ms, settings))
        {
            if (format == "atom")
                new Atom10FeedFormatter(feed).WriteTo(writer);
            else
                new Rss20FeedFormatter(feed).WriteTo(writer);
        }
        return ms.ToArray();
    }
}
