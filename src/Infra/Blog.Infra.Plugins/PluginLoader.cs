namespace Blog.Infra.Plugins;

using System.Reflection;
using Blog.Core.Contract.Primitives.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public sealed class PluginLoader
{
    private readonly ILogger<PluginLoader> _logger;
    public List<IBlogPlugin> Loaded { get; } = new();

    public PluginLoader(ILogger<PluginLoader> logger) => _logger = logger;

    public void LoadFromDirectory(string directory, IServiceCollection services)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            return;
        }

        foreach (var dll in Directory.EnumerateFiles(directory, "*.dll"))
        {
            try
            {
                var asm = Assembly.LoadFrom(dll);
                foreach (var type in asm.GetTypes().Where(t =>
                             typeof(IBlogPlugin).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass))
                {
                    if (Activator.CreateInstance(type) is not IBlogPlugin plugin) continue;
                    plugin.ConfigureServices(services);
                    Loaded.Add(plugin);
                    _logger.LogInformation("Loaded plugin {Id} v{Version}", plugin.Id, plugin.Version);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load plugin {Dll}", dll);
            }
        }
    }
}
