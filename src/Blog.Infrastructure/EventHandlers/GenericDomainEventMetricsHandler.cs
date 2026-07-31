using Blog.Application.EventBus;
using Blog.Domain.Abstractions;
using Blog.Infrastructure.Observability;
using Microsoft.Extensions.Logging;

namespace Blog.Infrastructure.EventHandlers;

/// <summary>
/// Catches all domain events via open-generic registration is hard in MS.DI;
/// concrete handlers + bus dynamic path cover metrics. This helper is invoked from adapters.
/// </summary>
public static class DomainEventMetrics
{
    public static void Record(IDomainEvent e)
    {
        BlogMetrics.DomainEventsPublished.Add(1,
            new KeyValuePair<string, object?>("event", e.EventName));
    }
}

public sealed class PostCreatedLoggingHandler : IDomainEventHandler<Domain.Aggregates.Posts.Events.PostCreatedDomainEvent>
{
    private readonly ILogger<PostCreatedLoggingHandler> _log;
    public PostCreatedLoggingHandler(ILogger<PostCreatedLoggingHandler> log) => _log = log;

    public Task HandleAsync(Domain.Aggregates.Posts.Events.PostCreatedDomainEvent domainEvent, CancellationToken ct = default)
    {
        DomainEventMetrics.Record(domainEvent);
        _log.LogInformation("DomainEvent {Event} Post={Slug}", domainEvent.EventName, domainEvent.Slug);
        return Task.CompletedTask;
    }
}
