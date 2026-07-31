namespace Blog.Core.Contract.Primitives.Messaging;

using Blog.Core.Domain.DomainEvents;

/// <summary>Port for publishing domain events (implemented in Infra.Messaging).</summary>
public interface IEventPublisher
{
    Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
    Task PublishAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
