using System.Text.Encodings.Web;
using System.Text.Json;
using BlogApp.Models;

namespace BlogApp.Services;

/// <summary>
/// Centralizes SEO (search engines) and AEO (answer engines / AI crawlers) concerns:
/// canonical URLs, Open Graph / Twitter Card values, and JSON-LD structured data
/// (BlogPosting, BreadcrumbList, WebSite+SearchAction). Controllers build a small
/// SeoMeta/JSON-LD payload and pass it to the view; the view only renders tags.
/// </summary>
public class SeoService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // keep URLs/quotes readable in the <script> tag
        WriteIndented = false
    };

    private readonly IConfiguration _config;

    public SeoService(IConfiguration config) => _config = config;

    public string SiteName => _config["Seo:SiteName"] ?? "Blog";
    public string SiteDescription => _config["Seo:SiteDescription"] ?? string.Empty;
    public string AuthorName => _config["Seo:AuthorName"] ?? SiteName;
    public string TwitterHandle => _config["Seo:TwitterHandle"] ?? string.Empty;

    /// <summary>WebSite + SearchAction schema for the homepage — helps answer engines and
    /// search engines understand the site is searchable (sitelinks search box eligibility).</summary>
    public string BuildWebsiteJsonLd(string baseUrl)
    {
        var schema = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Blog",
            ["name"] = SiteName,
            ["description"] = SiteDescription,
            ["url"] = baseUrl,
            ["publisher"] = new Dictionary<string, object?>
            {
                ["@type"] = "Organization",
                ["name"] = SiteName
            }
        };
        return JsonSerializer.Serialize(schema, JsonOptions);
    }

    /// <summary>BlogPosting schema for a post's Details page.</summary>
    public string BuildPostJsonLd(Post post, string canonicalUrl, string? imageUrl)
    {
        var schema = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BlogPosting",
            ["headline"] = post.Title,
            ["description"] = post.Summary,
            ["datePublished"] = (post.PublishedAtUtc ?? post.CreatedAtUtc).ToString("o"),
            ["dateModified"] = post.UpdatedAtUtc.ToString("o"),
            ["mainEntityOfPage"] = new Dictionary<string, object?>
            {
                ["@type"] = "WebPage",
                ["@id"] = canonicalUrl
            },
            ["author"] = new Dictionary<string, object?>
            {
                ["@type"] = "Person",
                ["name"] = AuthorName
            },
            ["publisher"] = new Dictionary<string, object?>
            {
                ["@type"] = "Organization",
                ["name"] = SiteName
            }
        };
        if (!string.IsNullOrEmpty(imageUrl)) schema["image"] = imageUrl;
        if (post.Category != null) schema["articleSection"] = post.Category.Name;

        return JsonSerializer.Serialize(schema, JsonOptions);
    }

    /// <summary>BreadcrumbList schema — small AEO/SEO win, helps answer engines place the
    /// page in site context (Home › Category › Post).</summary>
    public string BuildBreadcrumbJsonLd(params (string Name, string Url)[] items)
    {
        var listItems = items.Select((item, i) => new Dictionary<string, object?>
        {
            ["@type"] = "ListItem",
            ["position"] = i + 1,
            ["name"] = item.Name,
            ["item"] = item.Url
        });

        var schema = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BreadcrumbList",
            ["itemListElement"] = listItems
        };
        return JsonSerializer.Serialize(schema, JsonOptions);
    }
}
