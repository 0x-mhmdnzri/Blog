using System.Reflection;
using Microsoft.Extensions.Logging;

namespace AVICRM.Developer.Plugins;

public interface IBlogPlugin
{
    string Id { get; }
    string Name { get; }
    string Version { get; }
    void ConfigureServices(IServiceCollection services);
    Task StartAsync(IServiceProvider services, CancellationToken ct = default);
}

public sealed class PluginLoader
{
    private readonly ILogger<PluginLoader> _log;
    public List<IBlogPlugin> Loaded { get; } = new();

    public PluginLoader(ILogger<PluginLoader> log) => _log = log;

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
                    _log.LogInformation("Loaded plugin {Id} v{Version} from {Dll}", plugin.Id, plugin.Version, Path.GetFileName(dll));
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed to load plugin assembly {Dll}", dll);
            }
        }
    }

    public async Task StartAllAsync(IServiceProvider sp, CancellationToken ct = default)
    {
        foreach (var p in Loaded)
        {
            try
            {
                await p.StartAsync(sp, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Plugin {Id} StartAsync failed", p.Id);
            }
        }
    }
}
