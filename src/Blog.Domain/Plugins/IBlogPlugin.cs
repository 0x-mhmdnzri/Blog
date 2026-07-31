namespace Blog.Domain.Plugins;

/// <summary>Contract every loadable extension must implement.</summary>
public interface IBlogPlugin
{
    string Id { get; }
    string Name { get; }
    string Version { get; }
    string? Description { get; }

    /// <summary>Called once after DI registration.</summary>
    Task InitializeAsync(IPluginHost host, CancellationToken ct = default);
}

public interface IPluginHost
{
    IServiceProvider Services { get; }
    void RegisterWidget(Widgets.IWidgetDescriptor widget);
    void SubscribeDomainEvent<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : Abstractions.IDomainEvent;
}
