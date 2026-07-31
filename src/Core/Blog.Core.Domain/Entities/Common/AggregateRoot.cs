namespace Blog.Core.Domain.Entities.Common;

using DomainEvents;

/// <summary>Aggregate root with domain-event collection (Artix.API pattern).</summary>
public abstract class AggregateRoot : BaseEntity
{
    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent @event)
    {
        _domainEvents.Add(@event);
        ModifiedAt = DateTime.UtcNow;
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}
