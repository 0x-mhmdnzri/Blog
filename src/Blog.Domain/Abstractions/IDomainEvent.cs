namespace Blog.Domain.Abstractions;

/// <summary>Immutable fact that occurred inside the domain.</summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredAtUtc { get; }
    string EventName { get; }
}

public abstract record DomainEventBase : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
    public abstract string EventName { get; }
}
