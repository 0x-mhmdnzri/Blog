using Blog.Domain.Plugins;

namespace Blog.Application.Plugins;

public interface IPluginRegistry
{
    IReadOnlyList<IBlogPlugin> Plugins { get; }
    void Register(IBlogPlugin plugin);
    Task InitializeAllAsync(CancellationToken ct = default);
    IBlogPlugin? GetById(string id);
}

public sealed class PluginRegistry : IPluginRegistry
{
    private readonly List<IBlogPlugin> _plugins = new();
    private readonly IServiceProvider _services;
    private readonly Domain.Plugins.IPluginHost _host;

    public PluginRegistry(IServiceProvider services, Domain.Plugins.IPluginHost host)
    {
        _services = services;
        _host = host;
    }

    public IReadOnlyList<IBlogPlugin> Plugins => _plugins.AsReadOnly();

    public void Register(IBlogPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        if (_plugins.Any(p => string.Equals(p.Id, plugin.Id, StringComparison.OrdinalIgnoreCase)))
            return;
        _plugins.Add(plugin);
    }

    public async Task InitializeAllAsync(CancellationToken ct = default)
    {
        foreach (var p in _plugins)
            await p.InitializeAsync(_host, ct);
    }

    public IBlogPlugin? GetById(string id) =>
        _plugins.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
}
