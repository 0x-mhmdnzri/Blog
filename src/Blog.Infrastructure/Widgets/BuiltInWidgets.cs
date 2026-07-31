using System.Text;
using Blog.Domain.Widgets;

namespace Blog.Infrastructure.Widgets;

/// <summary>Built-in sample widgets (theme system companion).</summary>
public sealed class ReadingTipsWidget : IWidgetDescriptor
{
    public string Id => "builtin.reading-tips";
    public string DisplayName => "نکات مطالعه";
    public IReadOnlyList<string> Zones { get; } = new[] { "sidebar", "post-bottom" };
    public int Order => 100;

    public Task<WidgetRenderResult> RenderAsync(WidgetRenderContext context, CancellationToken ct = default)
    {
        var html = """
            <aside class="card-surface p-3 mb-3 widget-reading-tips" data-widget="builtin.reading-tips">
              <strong class="d-block mb-1">نکته مطالعه</strong>
              <p class="small text-muted-dark mb-0">برای تمرکز بهتر، حالت مطالعه و اندازه فونت را از نوار دسترسی تنظیم کنید.</p>
            </aside>
            """;
        return Task.FromResult(WidgetRenderResult.FromHtml(html, cacheable: true, cacheSeconds: 300));
    }
}

public sealed class HealthStatusWidget : IWidgetDescriptor
{
    public string Id => "builtin.health-badge";
    public string DisplayName => "وضعیت سیستم";
    public IReadOnlyList<string> Zones { get; } = new[] { "admin-dashboard" };
    public int Order => 10;

    public Task<WidgetRenderResult> RenderAsync(WidgetRenderContext context, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.Append("<div class=\"card-surface p-3 widget-health\" data-widget=\"builtin.health-badge\">");
        sb.Append("<strong>سلامت سیستم</strong>");
        sb.Append("<div class=\"small text-muted-dark mt-1\"><a href=\"/healthz\" class=\"link-accent\">/healthz</a> · <a href=\"/metrics\" class=\"link-accent\">/metrics</a></div>");
        sb.Append("</div>");
        return Task.FromResult(WidgetRenderResult.FromHtml(sb.ToString(), cacheable: false));
    }
}
