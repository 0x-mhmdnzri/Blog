using System.Text.Encodings.Web;
using System.Text.Json;
using BlogApp.Models;
using Microsoft.Extensions.DependencyInjection;

namespace BlogApp.Services;

/// <summary>
/// Centralizes SEO metadata. Site name/description prefer DB SiteSettings when available,
/// falling back to appsettings Seo section.
/// </summary>
public class SeoService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopeFactory;

    public SeoService(IConfiguration config, IServiceScopeFactory scopeFactory)
    {
        _config = config;
        _scopeFactory = scopeFactory;
    }

    public string SiteName => Resolve(SiteSettingKeys.SiteName, "Seo:SiteName") ?? "Blog";
    public string SiteDescription => Resolve(SiteSettingKeys.SiteDescription, "Seo:SiteDescription") ?? string.Empty;
    public string AuthorName => Resolve(SiteSettingKeys.AuthorName, "Seo:AuthorName") ?? SiteName;
    public string TwitterHandle => Resolve(SiteSettingKeys.TwitterHandle, "Seo:TwitterHandle") ?? string.Empty;

    private string? Resolve(string dbKey, string configKey)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var site = scope.ServiceProvider.GetService<ISiteConfigService>();
            if (site is not null)
            {
                var v = site.GetAsync(dbKey).GetAwaiter().GetResult();
                if (!string.IsNullOrWhiteSpace(v)) return v;
            }
        }
        catch
        {
            /* DB may not be ready during early bootstrap */
        }

        return _config[configKey];
    }

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
