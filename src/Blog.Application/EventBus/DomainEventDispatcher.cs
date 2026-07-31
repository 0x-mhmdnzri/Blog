using Blog.Domain.Abstractions;
using Microsoft.Extensions.Logging;

namespace Blog.Application.EventBus;

public sealed class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IDomainEventBus _bus;
    private readonly ILogger<DomainEventDispatcher> _log;

    public DomainEventDispatcher(IDomainEventBus bus, ILogger<DomainEventDispatcher> log)
    {
        _bus = bus;
        _log = log;
    }

    public Task DispatchAsync(AggregateRoot aggregate, CancellationToken ct = default)
    {
        var events = aggregate.DequeueDomainEvents();
        return DispatchAsync(events, ct);
    }

    public async Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default)
    {
        foreach (var e in events)
        {
            _log.LogDebug("Dispatching domain event {Event} Id={Id}", e.EventName, e.EventId);
            await _bus.PublishAsync(e, ct);
        }
    }
}
