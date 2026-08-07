using BlogApp.Data;
using BlogApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BlogApp.Services.Seo;

/// <summary>
/// P2.2 — on publish, place the post on high-authority internal hubs
/// (home featured strip + footer "Latest") so discovery is not sitemap-only.
/// </summary>
public interface IHubLinkOnPublishService
{
    Task EnsureHubLinksAsync(int postId, CancellationToken ct = default);
}

public sealed class HubLinkOnPublishService : IHubLinkOnPublishService
{
    public const int FeaturedWindowDays = 14;
    public const int MaxFeaturedPerLanguage = 12;

    private readonly IServiceScopeFactory _scopes;
    private readonly IMemoryCache _cache;
    private readonly ILogger<HubLinkOnPublishService> _log;

    public HubLinkOnPublishService(
        IServiceScopeFactory scopes,
        IMemoryCache cache,
        ILogger<HubLinkOnPublishService> log)
    {
        _scopes = scopes;
        _cache = cache;
        _log = log;
    }

    public async Task EnsureHubLinksAsync(int postId, CancellationToken ct = default)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var post = await db.Posts.AsTracking()
            .FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted, ct);
        if (post is null || !post.IsPublished)
            return;

        var changed = false;

        if (!post.IsFeatured)
        {
            post.IsFeatured = true;
            changed = true;
            _log.LogInformation("P2.2 hub-link: featured PostId={Id} Slug={Slug}", post.Id, post.Slug);
        }

        post.UpdatedAtUtc = DateTime.UtcNow;

        var cutoff = DateTime.UtcNow.AddDays(-FeaturedWindowDays);
        var featured = await db.Posts.AsTracking()
            .Where(p => p.IsPublished && !p.IsDeleted && p.IsFeatured
                        && p.LanguageCode == post.LanguageCode)
            .OrderByDescending(p => p.IsSticky)
            .ThenByDescending(p => p.PublishedAtUtc ?? p.CreatedAtUtc)
            .ToListAsync(ct);

        var keep = 0;
        foreach (var f in featured)
        {
            if (f.Id == post.Id)
            {
                keep++;
                continue;
            }
            if (f.IsSticky)
            {
                keep++;
                continue;
            }
            var pub = f.PublishedAtUtc ?? f.CreatedAtUtc;
            if (pub < cutoff || keep >= MaxFeaturedPerLanguage)
            {
                f.IsFeatured = false;
                f.UpdatedAtUtc = DateTime.UtcNow;
                changed = true;
            }
            else
            {
                keep++;
            }
        }

        if (changed)
            await db.SaveChangesAsync(ct);

        _cache.Remove("footer-hubs-v1");
        _cache.Remove("footer-hubs-v2");
    }
}
