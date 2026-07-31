using System.Reflection;
using System.Runtime.Loader;
using Blog.Application.Plugins;
using Blog.Domain.Plugins;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Blog.Infrastructure.Plugins;

/// <summary>Loads IBlogPlugin implementations from ContentRoot/plugins/*.dll</summary>
public sealed class PluginLoader
{
    private readonly IHostEnvironment _env;
    private readonly IPluginRegistry _registry;
    private readonly ILogger<PluginLoader> _log;

    public PluginLoader(IHostEnvironment env, IPluginRegistry registry, ILogger<PluginLoader> log)
    {
        _env = env;
        _registry = registry;
        _log = log;
    }

    public void LoadFromDirectory(string? directory = null)
    {
        var dir = directory ?? Path.Combine(_env.ContentRootPath, "plugins");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            _log.LogInformation("Plugins directory created at {Dir}", dir);
            return;
        }

        foreach (var dll in Directory.GetFiles(dir, "*.dll", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var alc = new AssemblyLoadContext(Path.GetFileNameWithoutExtension(dll), isCollectible: true);
                using var fs = File.OpenRead(dll);
                var asm = alc.LoadFromStream(fs);
                foreach (var type in asm.GetTypes().Where(t =>
                             typeof(IBlogPlugin).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass))
                {
                    if (Activator.CreateInstance(type) is IBlogPlugin plugin)
                    {
                        _registry.Register(plugin);
                        _log.LogInformation("Loaded plugin {Id} v{Version} from {Dll}", plugin.Id, plugin.Version, Path.GetFileName(dll));
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed to load plugin assembly {Dll}", dll);
            }
        }
    }
}
