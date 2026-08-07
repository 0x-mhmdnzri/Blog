using BlogApp.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BlogApp.ViewComponents;

public sealed class FooterHubItem
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
}

public sealed class FooterHubsModel
{
    public List<FooterHubItem> Categories { get; set; } = new();
    public List<FooterHubItem> Series { get; set; } = new();
    public List<FooterHubItem> Authors { get; set; } = new();
}

/// <summary>P2.1 — sitewide hub links so taxonomy/series/authors stay ≤1 click from any page.</summary>
public class FooterHubsViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;

    public FooterHubsViewComponent(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var model = await _cache.GetOrCreateAsync("footer-hubs-v1", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            var now = DateTime.UtcNow;

            var cats = await _db.Categories.AsNoTracking()
                .Where(c => c.Posts.Any(p => p.IsPublished && !p.IsDeleted
                    && (p.ExpiresAtUtc == null || p.ExpiresAtUtc > now)))
                .OrderByDescending(c => c.Posts.Count(p => p.IsPublished && !p.IsDeleted))
                .Take(8)
                .Select(c => new FooterHubItem
                {
                    Name = c.Name,
                    Url = "/?category=" + Uri.EscapeDataString(c.Slug)
                })
                .ToListAsync();

            var series = await _db.PostSeries.AsNoTracking()
                .Where(s => s.Posts.Any(sp => sp.Post.IsPublished && !sp.Post.IsDeleted
                    && (sp.Post.ExpiresAtUtc == null || sp.Post.ExpiresAtUtc > now)))
                .OrderByDescending(s => s.Posts.Count(sp => sp.Post.IsPublished && !sp.Post.IsDeleted))
                .Take(6)
                .Select(s => new FooterHubItem
                {
                    Name = s.Name,
                    Url = "/series/" + Uri.EscapeDataString(s.Slug)
                })
                .ToListAsync();

            var authors = await _db.Posts.AsNoTracking()
                .Where(p => p.IsPublished && !p.IsDeleted && p.Author != null && p.Author.UserName != null)
                .GroupBy(p => p.Author!.UserName!)
                .Select(g => new { UserName = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(6)
                .ToListAsync();

            return new FooterHubsModel
            {
                Categories = cats,
                Series = series,
                Authors = authors.Select(a => new FooterHubItem
                {
                    Name = a.UserName,
                    Url = "/author/" + Uri.EscapeDataString(a.UserName)
                }).ToList()
            };
        }) ?? new FooterHubsModel();

        return View(model);
    }
}
