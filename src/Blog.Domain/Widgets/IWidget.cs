namespace Blog.Domain.Widgets;

/// <summary>Custom widget that can be rendered in theme zones.</summary>
public interface IWidgetDescriptor
{
    string Id { get; }
    string DisplayName { get; }
    /// <summary>Zone keys: sidebar, footer, post-bottom, home-hero, admin-dashboard</summary>
    IReadOnlyList<string> Zones { get; }
    int Order { get; }

    Task<WidgetRenderResult> RenderAsync(WidgetRenderContext context, CancellationToken ct = default);
}

public sealed class WidgetRenderContext
{
    public required string Zone { get; init; }
    public string? Culture { get; init; }
    public string? UserId { get; init; }
    public IReadOnlyDictionary<string, string>? RouteValues { get; init; }
    public IServiceProvider Services { get; init; } = null!;
}

public sealed class WidgetRenderResult
{
    public string Html { get; init; } = string.Empty;
    public bool Cacheable { get; init; }
    public int CacheSeconds { get; init; } = 60;

    public static WidgetRenderResult Empty => new();
    public static WidgetRenderResult FromHtml(string html, bool cacheable = true, int cacheSeconds = 60) =>
        new() { Html = html ?? string.Empty, Cacheable = cacheable, CacheSeconds = cacheSeconds };
}
