using System.ComponentModel.DataAnnotations;
using BlogApp.Models;
using BlogApp.Services.Seo;

namespace BlogApp.Models.ViewModels;

public class SeoToolsViewModel
{
    public SeoMetaForm Meta { get; set; } = new();
    public RedirectForm NewRedirect { get; set; } = new();
    public List<RedirectRule> Redirects { get; set; } = new();
    public List<BrokenLinkReport> BrokenLinks { get; set; } = new();
    public List<SeoPostHealthItem> PostHealth { get; set; } = new();
    public int PublishedCount { get; set; }
    public int MissingSummaryCount { get; set; }
    public int MissingCoverCount { get; set; }
    public string SitemapUrl { get; set; } = "/sitemap.xml";
    public string RobotsUrl { get; set; } = "/robots.txt";
    public string? ActiveTab { get; set; }

    // IndexNow status (read-only for UI)
    public bool IndexNowEnabled { get; set; }
    public bool IndexNowHasKey { get; set; }
    public string? IndexNowKeyHint { get; set; }
    public string? IndexNowKeyUrl { get; set; }

    /// <summary>P0.1 bot crawl log summary (null until tab=crawl loads).</summary>
    public BotCrawlSummaryDto? Crawl { get; set; }
}

public class SeoMetaForm
{
    [MaxLength(120)]
    public string SiteName { get; set; } = "";

    [MaxLength(400)]
    public string SiteDescription { get; set; } = "";

    [MaxLength(120)]
    public string AuthorName { get; set; } = "";

    [MaxLength(80)]
    public string TwitterHandle { get; set; } = "";

    [MaxLength(200)]
    public string BaseUrl { get; set; } = "";

    /// <summary>Optional full robots.txt body. Empty = generated default.</summary>
    [MaxLength(4000)]
    public string? RobotsTxt { get; set; }
}

public class RedirectForm
{
    [Required, MaxLength(500)]
    public string FromPath { get; set; } = "";

    [Required, MaxLength(1000)]
    public string ToUrl { get; set; } = "";

    public int StatusCode { get; set; } = 301;

    [MaxLength(300)]
    public string? Notes { get; set; }
}

public class SeoPostHealthItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public bool HasSummary { get; set; }
    public bool HasCover { get; set; }
    public int Score { get; set; }
}

public class SeoImportForm
{
    /// <summary>wordpress | ghost</summary>
    [Required]
    public string Format { get; set; } = "wordpress";

    public string LanguageCode { get; set; } = "fa";
    public bool CreateRedirects { get; set; } = true;
    public bool PublishImmediately { get; set; } = true;
}
