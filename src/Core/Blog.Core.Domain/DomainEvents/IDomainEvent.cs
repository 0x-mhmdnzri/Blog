namespace Blog.Core.Domain.DomainEvents;

/// <summary>Marker for domain events raised by aggregates (Artix-style EDD).</summary>
public interface IDomainEvent
{
    DateTime OccurredOnUtc { get; }
}

public abstract record DomainEventBase : IDomainEvent
{
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}
