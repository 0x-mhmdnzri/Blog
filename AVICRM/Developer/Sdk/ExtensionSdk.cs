namespace AVICRM.Developer.Sdk;

/// <summary>
/// Extension SDK — implement in a plugin DLL under <c>AVICRM/plugins/</c>.
/// FEATURES.md: Extension SDK, Plugin Architecture.
/// </summary>
public interface IBlogExtension
{
    string Id { get; }
    string Name { get; }
    string Version { get; }

    /// <summary>Called once while DI is still building (add services).</summary>
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>Called after host is built (register widgets, pipeline slots, etc.).</summary>
    Task StartAsync(IBlogHostContext host, CancellationToken cancellationToken = default);
}

/// <summary>In-process host surface exposed to extensions.</summary>
public interface IBlogHostContext
{
    IServiceProvider Services { get; }
    IConfiguration Configuration { get; }
    IHostEnvironment Environment { get; }
    Widgets.WidgetRegistry Widgets { get; }
    Middleware.PipelineExtensionRegistry Pipeline { get; }
    Plugins.PluginLoader Plugins { get; }
}

public sealed class BlogHostContext : IBlogHostContext
{
    public required IServiceProvider Services { get; init; }
    public required IConfiguration Configuration { get; init; }
    public required IHostEnvironment Environment { get; init; }
    public required Widgets.WidgetRegistry Widgets { get; init; }
    public required Middleware.PipelineExtensionRegistry Pipeline { get; init; }
    public required Plugins.PluginLoader Plugins { get; init; }
}
