using Blog.Application.EventBus;
using Blog.Application.Widgets;
using Blog.Domain.Abstractions;
using Blog.Domain.Plugins;
using Blog.Domain.Widgets;
using Blog.Infrastructure.EventBus;

namespace Blog.Infrastructure.Plugins;

public sealed class PluginHost : IPluginHost
{
    private readonly IServiceProvider _services;
    private readonly IWidgetRegistry _widgets;
    private readonly InProcessDomainEventBus _bus;

    public PluginHost(IServiceProvider services, IWidgetRegistry widgets, IDomainEventBus bus)
    {
        _services = services;
        _widgets = widgets;
        _bus = bus as InProcessDomainEventBus
              ?? throw new InvalidOperationException("Plugin host requires InProcessDomainEventBus.");
    }

    public IServiceProvider Services => _services;

    public void RegisterWidget(IWidgetDescriptor widget) => _widgets.Register(widget);

    public void SubscribeDomainEvent<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : IDomainEvent =>
        _bus.Subscribe(handler);
}
