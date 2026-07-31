using Blog.Application.EventBus;
using Blog.Domain.Aggregates.Posts.Events;
using Blog.Infrastructure.Observability;
using Microsoft.Extensions.Logging;

namespace Blog.Infrastructure.EventHandlers;

/// <summary>Sample domain event handler — metrics + structured log.</summary>
public sealed class PostPublishedLoggingHandler : IDomainEventHandler<PostPublishedDomainEvent>
{
    private readonly ILogger<PostPublishedLoggingHandler> _log;

    public PostPublishedLoggingHandler(ILogger<PostPublishedLoggingHandler> log) => _log = log;

    public Task HandleAsync(PostPublishedDomainEvent domainEvent, CancellationToken ct = default)
    {
        BlogMetrics.PostsPublished.Add(1);
        BlogMetrics.DomainEventsPublished.Add(1, new KeyValuePair<string, object?>("event", domainEvent.EventName));

        using var activity = BlogMetrics.ActivitySource.StartActivity("domain.post.published");
        activity?.SetTag("post.id", domainEvent.PostId.ToString());
        activity?.SetTag("post.slug", domainEvent.Slug);

        _log.LogInformation(
            "DomainEvent {EventName} PostId={PostId} Slug={Slug} Author={Author}",
            domainEvent.EventName, domainEvent.PostId, domainEvent.Slug, domainEvent.AuthorId);

        return Task.CompletedTask;
    }
}
