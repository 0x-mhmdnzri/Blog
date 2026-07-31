using System.Diagnostics.Metrics;

namespace BlogApp.Developer.Observability;

public static class BlogMetrics
{
    public static readonly Meter Meter = new("BlogApp", "1.0.0");

    public static readonly Counter<long> PostsPublished =
        Meter.CreateCounter<long>("blog_posts_published_total", description: "Posts published via EDD");

    public static readonly Counter<long> PostsCreated =
        Meter.CreateCounter<long>("blog_posts_created_total", description: "Posts created via EDD");

    public static readonly Counter<long> CommentsApproved =
        Meter.CreateCounter<long>("blog_comments_approved_total", description: "Comments approved via EDD");

    public static readonly Counter<long> AuthorFollows =
        Meter.CreateCounter<long>("blog_author_follows_total", description: "Author follow events");

    public static readonly Counter<long> DomainEventsPublished =
        Meter.CreateCounter<long>("blog_domain_events_published_total", description: "Domain events published");
}
