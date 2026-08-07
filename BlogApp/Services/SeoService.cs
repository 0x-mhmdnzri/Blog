using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using BlogApp.Models;
using Microsoft.Extensions.DependencyInjection;

namespace BlogApp.Services;

/// <summary>
/// JSON-LD (https://json-ld.org/) + SEO metadata for Google SEO and AEO.
/// Site name/description prefer DB SiteSettings when available.
/// </summary>
public class SeoService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
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
    public string? DefaultOgImageUrl => _config["Seo:DefaultOgImageUrl"];
    public string? ContactEmail => _config["Seo:ContactEmail"];
    public string InLanguage => _config["Seo:InLanguage"] ?? "fa-IR";

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

    private static string Serialize(object obj) => JsonSerializer.Serialize(obj, JsonOptions);

    private static string BaseFromUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var u))
            return u.GetLeftPart(UriPartial.Authority);
        return url.TrimEnd('/');
    }

    public Dictionary<string, object?> BuildOrganization(string baseUrl)
    {
        baseUrl = baseUrl.TrimEnd('/');
        var org = new Dictionary<string, object?>
        {
            ["@type"] = "Organization",
            ["@id"] = baseUrl + "/#organization",
            ["name"] = SiteName,
            ["url"] = baseUrl + "/",
            ["description"] = string.IsNullOrWhiteSpace(SiteDescription) ? null : SiteDescription
        };
        if (!string.IsNullOrWhiteSpace(DefaultOgImageUrl))
        {
            var logo = DefaultOgImageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? DefaultOgImageUrl
                : baseUrl + "/" + DefaultOgImageUrl.TrimStart('/');
            org["logo"] = new Dictionary<string, object?>
            {
                ["@type"] = "ImageObject",
                ["url"] = logo
            };
        }
        if (!string.IsNullOrWhiteSpace(ContactEmail))
            org["email"] = ContactEmail;
        if (!string.IsNullOrWhiteSpace(TwitterHandle))
            org["sameAs"] = new[] { "https://twitter.com/" + TwitterHandle.TrimStart('@') };
        return org;
    }

    public string BuildWebsiteJsonLd(string baseUrl)
    {
        baseUrl = baseUrl.TrimEnd('/');
        var org = BuildOrganization(baseUrl);
        var graph = new List<Dictionary<string, object?>>
        {
            org,
            new()
            {
                ["@type"] = "WebSite",
                ["@id"] = baseUrl + "/#website",
                ["name"] = SiteName,
                ["url"] = baseUrl + "/",
                ["description"] = string.IsNullOrWhiteSpace(SiteDescription) ? null : SiteDescription,
                ["inLanguage"] = InLanguage,
                ["publisher"] = new Dictionary<string, object?> { ["@id"] = org["@id"] },
                ["potentialAction"] = new Dictionary<string, object?>
                {
                    ["@type"] = "SearchAction",
                    ["target"] = new Dictionary<string, object?>
                    {
                        ["@type"] = "EntryPoint",
                        ["urlTemplate"] = baseUrl + "/?q={search_term_string}"
                    },
                    ["query-input"] = "required name=search_term_string"
                }
            }
        };
        return Serialize(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@graph"] = graph
        });
    }

    public string BuildCollectionJsonLd(
        string baseUrl,
        string pageUrl,
        string name,
        string? description,
        IEnumerable<(string Title, string Url, string? DatePublished)> items)
    {
        baseUrl = baseUrl.TrimEnd('/');
        var list = items.Select((it, i) => new Dictionary<string, object?>
        {
            ["@type"] = "ListItem",
            ["position"] = i + 1,
            ["url"] = it.Url,
            ["name"] = it.Title,
            ["item"] = new Dictionary<string, object?>
            {
                ["@type"] = "BlogPosting",
                ["headline"] = it.Title,
                ["url"] = it.Url,
                ["datePublished"] = it.DatePublished
            }
        }).ToList();

        return Serialize(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "CollectionPage",
            ["@id"] = pageUrl.TrimEnd('#') + "#webpage",
            ["url"] = pageUrl,
            ["name"] = name,
            ["description"] = description ?? SiteDescription,
            ["inLanguage"] = InLanguage,
            ["isPartOf"] = new Dictionary<string, object?> { ["@id"] = baseUrl + "/#website" },
            ["mainEntity"] = new Dictionary<string, object?>
            {
                ["@type"] = "ItemList",
                ["numberOfItems"] = list.Count,
                ["itemListElement"] = list
            }
        });
    }

    public string BuildPostJsonLd(Post post, string canonicalUrl, string? imageUrl)
    {
        var baseUrl = BaseFromUrl(canonicalUrl);
        var org = BuildOrganization(baseUrl);

        var authorName = post.Author != null
            ? (string.IsNullOrWhiteSpace(post.Author.DisplayName) ? post.Author.UserName : post.Author.DisplayName)
            : AuthorName;
        var authorUrl = post.Author?.UserName != null
            ? baseUrl + "/author/" + Uri.EscapeDataString(post.Author.UserName)
            : null;

        var authorNode = new Dictionary<string, object?>
        {
            ["@type"] = "Person",
            ["name"] = authorName,
            ["url"] = authorUrl
        };
        if (post.Author?.UserName != null)
            authorNode["@id"] = baseUrl + "/author/" + Uri.EscapeDataString(post.Author.UserName) + "#person";

        var keywords = post.PostTags?
            .Select(pt => pt.Tag?.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Cast<string>()
            .ToList() ?? new List<string>();

        var published = (post.PublishedAtUtc ?? post.CreatedAtUtc).ToUniversalTime()
            .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        var modified = post.UpdatedAtUtc.ToUniversalTime()
            .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

        var article = new Dictionary<string, object?>
        {
            ["@type"] = new[] { "BlogPosting", "Article" },
            ["@id"] = canonicalUrl + "#article",
            ["mainEntityOfPage"] = new Dictionary<string, object?>
            {
                ["@type"] = "WebPage",
                ["@id"] = canonicalUrl
            },
            ["headline"] = post.Title,
            ["name"] = post.Title,
            ["url"] = canonicalUrl,
            ["datePublished"] = published,
            ["dateModified"] = modified,
            ["description"] = post.Summary ?? Truncate(StripMd(post.ContentMarkdown), 300),
            ["articleBody"] = Truncate(StripMd(post.ContentMarkdown), 5000),
            ["inLanguage"] = string.IsNullOrWhiteSpace(post.LanguageCode)
                ? InLanguage
                : (post.LanguageCode.Contains('-') ? post.LanguageCode : post.LanguageCode + "-IR"),
            ["isAccessibleForFree"] = !post.IsPremium,
            ["author"] = authorNode,
            ["publisher"] = new Dictionary<string, object?> { ["@id"] = org["@id"] },
            ["isPartOf"] = new Dictionary<string, object?> { ["@id"] = baseUrl + "/#website" },
            ["speakable"] = new Dictionary<string, object?>
            {
                ["@type"] = "SpeakableSpecification",
                ["cssSelector"] = new[]
                {
                    "h1.post-title", "h1", ".post-summary",
                    "article .readme-content p:first-of-type",
                    "article .post-body-island p:first-of-type"
                }
            }
        };

        if (!string.IsNullOrEmpty(imageUrl))
        {
            article["image"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["@type"] = "ImageObject",
                    ["url"] = imageUrl,
                    ["width"] = 1200,
                    ["height"] = 630
                }
            };
        }
        else if (!string.IsNullOrWhiteSpace(DefaultOgImageUrl))
        {
            article["image"] = DefaultOgImageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? DefaultOgImageUrl
                : baseUrl + "/" + DefaultOgImageUrl.TrimStart('/');
        }

        if (post.Category != null)
            article["articleSection"] = post.Category.Name;
        if (keywords.Count > 0)
            article["keywords"] = string.Join(", ", keywords);
        if (post.ReadingTimeMinutes > 0)
        {
            article["timeRequired"] = $"PT{post.ReadingTimeMinutes}M";
            article["wordCount"] = Math.Max(100, post.ReadingTimeMinutes * 200);
        }

        // P3.2 — demand signal for Google (views as InteractionCounter)
        if (post.ViewCount > 0)
        {
            article["interactionStatistic"] = new Dictionary<string, object?>
            {
                ["@type"] = "InteractionCounter",
                ["interactionType"] = "https://schema.org/ReadAction",
                ["userInteractionCount"] = post.ViewCount
            };
        }

        return Serialize(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@graph"] = new List<Dictionary<string, object?>> { org, article }
        });
    }

    public string BuildBreadcrumbJsonLd(params (string Name, string Url)[] items)
    {
        var list = items.Select((it, i) => new Dictionary<string, object?>
        {
            ["@type"] = "ListItem",
            ["position"] = i + 1,
            ["name"] = it.Name,
            ["item"] = it.Url
        }).ToList();

        var lastUrl = items.Length > 0 ? items[^1].Url : "";
        return Serialize(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BreadcrumbList",
            ["@id"] = string.IsNullOrEmpty(lastUrl) ? null : lastUrl + "#breadcrumb",
            ["itemListElement"] = list
        });
    }

    public string BuildPersonJsonLd(
        string baseUrl,
        ApplicationUser user,
        string profileUrl,
        int postCount,
        string? imageUrl)
    {
        baseUrl = baseUrl.TrimEnd('/');
        var name = string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName;

        var person = new Dictionary<string, object?>
        {
            ["@type"] = "Person",
            ["@id"] = profileUrl + "#person",
            ["name"] = name,
            ["url"] = profileUrl,
            ["description"] = user.Bio,
            ["identifier"] = user.UserName
        };
        if (!string.IsNullOrEmpty(imageUrl)) person["image"] = imageUrl;
        if (postCount > 0)
            person["interactionStatistic"] = new Dictionary<string, object?>
            {
                ["@type"] = "InteractionCounter",
                ["interactionType"] = "https://schema.org/WriteAction",
                ["userInteractionCount"] = postCount
            };

        var page = new Dictionary<string, object?>
        {
            ["@type"] = "ProfilePage",
            ["@id"] = profileUrl + "#webpage",
            ["url"] = profileUrl,
            ["name"] = name,
            ["description"] = user.Bio ?? $"Posts by {name}",
            ["inLanguage"] = InLanguage,
            ["mainEntity"] = new Dictionary<string, object?> { ["@id"] = person["@id"] },
            ["isPartOf"] = new Dictionary<string, object?> { ["@id"] = baseUrl + "/#website" }
        };

        return Serialize(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@graph"] = new[] { person, page, BuildOrganization(baseUrl) }
        });
    }

    private static string StripMd(string? md)
    {
        if (string.IsNullOrWhiteSpace(md)) return "";
        var s = Regex.Replace(md, @"[#>*\`\[\]\(\)_~-]+", " ");
        s = Regex.Replace(s, @"\s+", " ").Trim();
        return s;
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
        return s[..(max - 1)].TrimEnd() + "…";
    }
}
