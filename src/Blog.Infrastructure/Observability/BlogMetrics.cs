using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Blog.Infrastructure.Observability;

/// <summary>Application metrics exposed via OpenTelemetry / Prometheus.</summary>
public static class BlogMetrics
{
    public const string MeterName = "BlogApp";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly Counter<long> DomainEventsPublished =
        Meter.CreateCounter<long>("blog.domain_events.published", description: "Domain events published");

    public static readonly Counter<long> PostsPublished =
        Meter.CreateCounter<long>("blog.posts.published", description: "Posts published");

    public static readonly Counter<long> ApiRequests =
        Meter.CreateCounter<long>("blog.api.requests", description: "API requests");

    public static readonly Histogram<double> RequestDurationMs =
        Meter.CreateHistogram<double>("blog.request.duration_ms", unit: "ms", description: "HTTP request duration");

    public static readonly ActivitySource ActivitySource = new("BlogApp", "1.0.0");
}
