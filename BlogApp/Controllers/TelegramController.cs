using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

/// <summary>Lightweight Instant View–friendly reader for Telegram and other share bots.</summary>
public class TelegramController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly MarkdownService _markdown;
    private readonly ISiteConfigService _site;

    public TelegramController(ApplicationDbContext db, MarkdownService markdown, ISiteConfigService site)
    {
        _db = db;
        _markdown = markdown;
        _site = site;
    }

    /// <summary>GET /iv/{slug} or /iv/{lang}/{slug} — minimal article HTML for Telegram Instant View.</summary>
    [HttpGet("/iv/{slug}")]
    [HttpGet("/iv/{lang}/{slug}")]
    [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "lang", "slug" })]
    public async Task<IActionResult> InstantView(string slug, string? lang = null)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 220)
            return NotFound();

        lang = string.IsNullOrWhiteSpace(lang) ? null : AppCultures.Normalize(lang);

        var query = _db.Posts.AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
            .Where(p => p.Slug == slug && p.IsPublished && !p.IsDeleted);

        if (lang != null)
            query = query.Where(p => p.LanguageCode == lang);

        var post = await query.FirstOrDefaultAsync();
        if (post is null)
        {
            post = await _db.Posts.AsNoTracking()
                .Include(p => p.Author)
                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
                .FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublished && !p.IsDeleted);
            if (post is null) return NotFound();
        }

        var baseUrl = await ResolveBaseUrlAsync();
        var canonical = $"{baseUrl}/{post.LanguageCode}/post/{post.Slug}";
        var siteName = await _site.GetAsync(SiteSettingKeys.SiteName) ?? "Blog";

        var html = _markdown.RenderToHtmlWithToc(
            post.ContentMarkdown ?? string.Empty,
            includeToc: false,
            cultureCode: post.LanguageCode);

        var vm = new TelegramInstantViewModel
        {
            Title = post.Title,
            Summary = post.Summary,
            HtmlBody = html,
            AuthorName = post.Author?.DisplayName ?? post.Author?.UserName ?? "",
            AuthorUserName = post.Author?.UserName,
            PublishedAtUtc = post.PublishedAtUtc ?? post.CreatedAtUtc,
            CanonicalUrl = canonical,
            SiteName = siteName,
            CoverUrl = post.CoverMediaAssetId is int mid ? $"{baseUrl}/media/{mid}" : null,
            LanguageCode = post.LanguageCode,
            ReadingTimeMinutes = Math.Max(1, post.ReadingTimeMinutes),
            Tags = post.PostTags?.Where(pt => pt.Tag != null).Select(pt => pt.Tag!.Name).ToList() ?? new()
        };

        return View("~/Views/Telegram/InstantView.cshtml", vm);
    }

    private async Task<string> ResolveBaseUrlAsync()
    {
        var configured = await _site.GetAsync(SiteSettingKeys.BaseUrl);
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.TrimEnd('/');
        return $"{Request.Scheme}://{Request.Host}";
    }
}
