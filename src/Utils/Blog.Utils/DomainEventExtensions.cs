namespace Blog.Utils;

using Blog.Core.Domain.Entities.Common;
using Blog.Core.Domain.DomainEvents;

public static class DomainEventExtensions
{
    public static IReadOnlyList<IDomainEvent> DrainEvents(this AggregateRoot aggregate)
    {
        var list = aggregate.DomainEvents.ToList();
        aggregate.ClearDomainEvents();
        return list;
    }
}
