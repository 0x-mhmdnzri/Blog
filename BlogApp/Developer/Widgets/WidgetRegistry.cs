using BlogApp.Data;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Developer.Widgets;

public interface IWidget
{
    string Id { get; }
    string Title { get; }
    /// <summary>sidebar | footer | home-top | home-bottom</summary>
    string Zone { get; }
    int Order { get; }
    Task<string> RenderHtmlAsync(IServiceProvider services, CancellationToken ct = default);
}

public sealed class WidgetRegistry
{
    private readonly List<IWidget> _widgets = new();

    public void Register(IWidget widget)
    {
        ArgumentNullException.ThrowIfNull(widget);
        _widgets.RemoveAll(w => string.Equals(w.Id, widget.Id, StringComparison.OrdinalIgnoreCase));
        _widgets.Add(widget);
    }

    public IEnumerable<IWidget> ForZone(string zone) =>
        _widgets.Where(w => string.Equals(w.Zone, zone, StringComparison.OrdinalIgnoreCase))
            .OrderBy(w => w.Order);

    public IReadOnlyList<IWidget> All => _widgets.OrderBy(w => w.Zone).ThenBy(w => w.Order).ToList();
}

public sealed class PopularPostsWidget : IWidget
{
    public string Id => "builtin.popular-posts";
    public string Title => "Popular posts";
    public string Zone => "sidebar";
    public int Order => 10;

    public async Task<string> RenderHtmlAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var posts = await db.Posts.AsNoTracking()
            .Where(p => p.IsPublished && !p.IsDeleted)
            .OrderByDescending(p => p.ViewCount)
            .Take(5)
            .Select(p => new { p.Title, p.Slug, p.ViewCount })
            .ToListAsync(ct);

        if (posts.Count == 0)
            return "<div class=\"widget empty\">No posts yet</div>";

        var items = string.Join("", posts.Select(p =>
            $"<li><a href=\"/post/{System.Net.WebUtility.HtmlEncode(p.Slug)}\" dir=\"auto\">{System.Net.WebUtility.HtmlEncode(p.Title)}</a> <span class=\"ltr-field small\">{p.ViewCount}</span></li>"));
        return $"<div class=\"widget\"><h3 class=\"h6\">{Title}</h3><ul class=\"list-unstyled mb-0\">{items}</ul></div>";
    }
}

public sealed class RecentPostsWidget : IWidget
{
    public string Id => "builtin.recent-posts";
    public string Title => "Recent posts";
    public string Zone => "sidebar";
    public int Order => 20;

    public async Task<string> RenderHtmlAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var posts = await db.Posts.AsNoTracking()
            .Where(p => p.IsPublished && !p.IsDeleted)
            .OrderByDescending(p => p.PublishedAtUtc)
            .Take(5)
            .Select(p => new { p.Title, p.Slug })
            .ToListAsync(ct);

        if (posts.Count == 0)
            return "<div class=\"widget empty\">No posts yet</div>";

        var items = string.Join("", posts.Select(p =>
            $"<li><a href=\"/post/{System.Net.WebUtility.HtmlEncode(p.Slug)}\" dir=\"auto\">{System.Net.WebUtility.HtmlEncode(p.Title)}</a></li>"));
        return $"<div class=\"widget\"><h3 class=\"h6\">{Title}</h3><ul class=\"list-unstyled mb-0\">{items}</ul></div>";
    }
}
