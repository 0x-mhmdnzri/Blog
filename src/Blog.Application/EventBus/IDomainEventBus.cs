using Blog.Domain.Abstractions;

namespace Blog.Application.EventBus;

/// <summary>In-process / distributed domain event bus.</summary>
public interface IDomainEventBus
{
    Task PublishAsync(IDomainEvent domainEvent, CancellationToken ct = default);
    Task PublishManyAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default);
}

public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct = default);
}

/// <summary>Dispatches dequeued events from an aggregate after persistence.</summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(AggregateRoot aggregate, CancellationToken ct = default);
    Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default);
}
