namespace Blog.Infra.Messaging;

using Blog.Core.Contract.Primitives.Messaging;
using Blog.Core.Domain.DomainEvents;
using Microsoft.Extensions.Logging;

/// <summary>Artix-style event publisher (in-process; swap for RabbitMQ later).</summary>
public sealed class InProcessEventPublisher : IEventPublisher
{
    private readonly ILogger<InProcessEventPublisher> _logger;
    private readonly List<Func<IDomainEvent, CancellationToken, Task>> _handlers = new();

    public InProcessEventPublisher(ILogger<InProcessEventPublisher> logger) => _logger = logger;

    public void Subscribe(Func<IDomainEvent, CancellationToken, Task> handler) => _handlers.Add(handler);

    public async Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Domain event {Type}", domainEvent.GetType().Name);
        foreach (var h in _handlers)
            await h(domainEvent, cancellationToken);
    }

    public async Task PublishAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var e in domainEvents)
            await PublishAsync(e, cancellationToken);
    }
}
