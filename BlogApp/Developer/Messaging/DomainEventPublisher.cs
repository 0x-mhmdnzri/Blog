using BlogApp.Developer.Domain;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace BlogApp.Developer.Messaging;

public interface IDomainEventPublisher
{
    Task PublishAsync(IDomainEvent domainEvent, CancellationToken ct = default);
    Task PublishAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default);
    Task PublishIntegrationAsync<T>(T message, CancellationToken ct = default) where T : class, IIntegrationEvent;
}

/// <summary>
/// Bridges domain events → MassTransit integration messages.
/// Transport is InMemory when RabbitMq:HostName is empty; otherwise RabbitMQ.
/// </summary>
public sealed class MassTransitDomainEventPublisher : IDomainEventPublisher
{
    private readonly IBus _bus;
    private readonly ILogger<MassTransitDomainEventPublisher> _log;

    public MassTransitDomainEventPublisher(IBus bus, ILogger<MassTransitDomainEventPublisher> log)
    {
        _bus = bus;
        _log = log;
    }

    public Task PublishAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default) =>
        Task.WhenAll(events.Select(e => PublishAsync(e, ct)));

    public async Task PublishAsync(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        object? integration = domainEvent switch
        {
            PostPublishedDomainEvent e => new PostPublishedIntegrationEvent
            {
                EventId = e.EventId,
                OccurredOnUtc = e.OccurredOnUtc,
                PostId = e.PostId,
                Title = e.Title,
                Slug = e.Slug,
                AuthorId = e.AuthorId,
                PublishedAtUtc = e.PublishedAtUtc
            },
            PostCreatedDomainEvent e => new PostCreatedIntegrationEvent
            {
                EventId = e.EventId,
                OccurredOnUtc = e.OccurredOnUtc,
                PostId = e.PostId,
                Title = e.Title,
                Slug = e.Slug,
                AuthorId = e.AuthorId
            },
            CommentApprovedDomainEvent e => new CommentApprovedIntegrationEvent
            {
                EventId = e.EventId,
                OccurredOnUtc = e.OccurredOnUtc,
                CommentId = e.CommentId,
                PostId = e.PostId,
                PostSlug = e.PostSlug
            },
            AuthorFollowedDomainEvent e => new AuthorFollowedIntegrationEvent
            {
                EventId = e.EventId,
                OccurredOnUtc = e.OccurredOnUtc,
                FollowerUserId = e.FollowerUserId,
                AuthorUserId = e.AuthorUserId
            },
            _ => null
        };

        if (integration is null)
        {
            _log.LogDebug("No integration mapping for domain event {Name}", domainEvent.EventName);
            return;
        }

        await _bus.Publish(integration, ct);
        _log.LogInformation("Published {Event} via MassTransit EventId={Id}",
            domainEvent.EventName, domainEvent.EventId);
    }

    public async Task PublishIntegrationAsync<T>(T message, CancellationToken ct = default)
        where T : class, IIntegrationEvent
    {
        await _bus.Publish(message, ct);
        _log.LogInformation("Published integration {Type} EventId={Id}", typeof(T).Name, message.EventId);
    }
}
