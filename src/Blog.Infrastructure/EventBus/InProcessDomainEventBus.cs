using System.Collections.Concurrent;
using Blog.Application.EventBus;
using Blog.Domain.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Blog.Infrastructure.EventBus;

/// <summary>
/// In-process domain event bus with scoped handler resolution.
/// Also supports dynamic plugin subscriptions via <see cref="Subscribe"/>.
/// </summary>
public sealed class InProcessDomainEventBus : IDomainEventBus
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<InProcessDomainEventBus> _log;
    private readonly ConcurrentDictionary<Type, List<Func<object, CancellationToken, Task>>> _dynamic = new();

    public InProcessDomainEventBus(IServiceScopeFactory scopes, ILogger<InProcessDomainEventBus> log)
    {
        _scopes = scopes;
        _log = log;
    }

    public void Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : IDomainEvent
    {
        var list = _dynamic.GetOrAdd(typeof(TEvent), _ => new List<Func<object, CancellationToken, Task>>());
        lock (list)
        {
            list.Add((o, ct) => handler((TEvent)o, ct));
        }
    }

    public Task PublishManyAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default) =>
        Task.WhenAll(events.Select(e => PublishAsync(e, ct)));

    public async Task PublishAsync(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var eventType = domainEvent.GetType();

        await using var scope = _scopes.CreateAsyncScope();
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);
        var handlers = scope.ServiceProvider.GetServices(handlerType);

        foreach (var handler in handlers)
        {
            try
            {
                var method = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!;
                var task = (Task)method.Invoke(handler, new object[] { domainEvent, ct })!;
                await task;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Domain handler failed for {Event}", domainEvent.EventName);
            }
        }

        if (_dynamic.TryGetValue(eventType, out var dyn))
        {
            List<Func<object, CancellationToken, Task>> snapshot;
            lock (dyn) snapshot = dyn.ToList();
            foreach (var h in snapshot)
            {
                try { await h(domainEvent, ct); }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Dynamic domain handler failed for {Event}", domainEvent.EventName);
                }
            }
        }

        // Also try base interfaces (optional future)
        _log.LogDebug("Published {Event} {Id}", domainEvent.EventName, domainEvent.EventId);
    }
}
