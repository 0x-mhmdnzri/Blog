using System.Net.Http.Json;
using AVICRM.Data;
using AVICRM.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AVICRM.Services;

public class IndexNowOptions
{
    /// <summary>When false, pings are no-ops (log only).</summary>
    public bool Enabled { get; set; }

    /// <summary>IndexNow API key (also served at /{key}.txt for verification).</summary>
    public string Key { get; set; } = "";

    /// <summary>Comma-separated endpoints. Defaults include IndexNow + Bing.</summary>
    public string Endpoints { get; set; } =
        "https://api.indexnow.org/indexnow,https://www.bing.com/indexnow";

    public int TimeoutSeconds { get; set; } = 20;
}

public interface IIndexNowService
{
    Task NotifyUrlAsync(string absoluteUrl, CancellationToken ct = default);
    Task NotifyUrlsAsync(IEnumerable<string> absoluteUrls, CancellationToken ct = default);
    Task NotifyPostAsync(int postId, string slug, string? languageCode, CancellationToken ct = default);
    Task<int> SubmitAllPublishedAsync(CancellationToken ct = default);
}

/// <summary>
/// IndexNow + search-engine URL ping. Fire-and-forget safe; failures only log.
/// FEATURES.md SEO: IndexNow / search-engine ping on publish.
/// </summary>
public sealed class IndexNowService : IIndexNowService
{
    private readonly IndexNowOptions _opt;
    private readonly IHttpClientFactory _http;
    private readonly ISiteConfigService _site;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<IndexNowService> _log;

    public IndexNowService(
        IOptions<IndexNowOptions> opt,
        IHttpClientFactory http,
        ISiteConfigService site,
        IServiceScopeFactory scopes,
        ILogger<IndexNowService> log)
    {
        _opt = opt.Value;
        _http = http;
        _site = site;
        _scopes = scopes;
        _log = log;
    }

    public Task NotifyUrlAsync(string absoluteUrl, CancellationToken ct = default) =>
        NotifyUrlsAsync(new[] { absoluteUrl }, ct);

    public async Task NotifyPostAsync(int postId, string slug, string? languageCode, CancellationToken ct = default)
    {
        var lang = languageCode;
        if (string.IsNullOrWhiteSpace(lang))
        {
            await using var scope = _scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            lang = await db.Posts.AsNoTracking()
                .Where(p => p.Id == postId)
                .Select(p => p.LanguageCode)
                .FirstOrDefaultAsync(ct);
        }

        lang = string.IsNullOrWhiteSpace(lang) ? "fa" : lang.Trim();
        var baseUrl = await ResolveBaseUrlAsync();
        var url = $"{baseUrl}/{lang}/post/{slug}";
        await NotifyUrlsAsync(new[] { url }, ct);
    }

    public async Task NotifyUrlsAsync(IEnumerable<string> absoluteUrls, CancellationToken ct = default)
    {
        if (!_opt.Enabled)
        {
            _log.LogDebug("IndexNow disabled — skip ping");
            return;
        }

        if (string.IsNullOrWhiteSpace(_opt.Key))
        {
            _log.LogWarning("IndexNow enabled but Key is empty");
            return;
        }

        var urls = absoluteUrls
            .Where(u => !string.IsNullOrWhiteSpace(u) && u.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10000)
            .ToList();
        if (urls.Count == 0) return;

        var baseUrl = await ResolveBaseUrlAsync();
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            _log.LogWarning("IndexNow: invalid BaseUrl {Base}", baseUrl);
            return;
        }

        var host = baseUri.Host;
        var keyLocation = $"{baseUrl}/{_opt.Key}.txt";
        var payload = new
        {
            host,
            key = _opt.Key,
            keyLocation,
            urlList = urls
        };

        var endpoints = (_opt.Endpoints ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (endpoints.Length == 0)
            endpoints = new[] { "https://api.indexnow.org/indexnow" };

        var client = _http.CreateClient("IndexNow");
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(_opt.TimeoutSeconds, 5, 60));

        foreach (var ep in endpoints)
        {
            try
            {
                using var res = await client.PostAsJsonAsync(ep, payload, ct);
                _log.LogInformation(
                    "IndexNow POST {Endpoint} status={Status} urls={Count}",
                    ep, (int)res.StatusCode, urls.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogWarning(ex, "IndexNow POST failed {Endpoint}", ep);
            }
        }
    }

    public async Task<int> SubmitAllPublishedAsync(CancellationToken ct = default)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var baseUrl = await ResolveBaseUrlAsync();
        var now = DateTime.UtcNow;

        var posts = await db.Posts.AsNoTracking()
            .Where(p => p.IsPublished && !p.IsDeleted)
            .Where(p => p.ExpiresAtUtc == null || p.ExpiresAtUtc > now)
            .Where(p => p.TranslationStatus == Models.TranslationStatus.Original
                        || p.TranslationStatus == Models.TranslationStatus.Approved)
            .Select(p => new { p.Slug, p.LanguageCode })
            .ToListAsync(ct);

        var urls = posts
            .Select(p => $"{baseUrl}/{p.LanguageCode}/post/{p.Slug}")
            .ToList();

        urls.Insert(0, baseUrl + "/");
        foreach (var lang in new[] { "fa", "en", "ar" })
            urls.Insert(1, $"{baseUrl}/{lang}/");

        await NotifyUrlsAsync(urls, ct);
        return posts.Count;
    }

    private async Task<string> ResolveBaseUrlAsync()
    {
        var configured = await _site.GetAsync(SiteSettingKeys.BaseUrl);
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.TrimEnd('/');
        return "https://example.com";
    }
}
