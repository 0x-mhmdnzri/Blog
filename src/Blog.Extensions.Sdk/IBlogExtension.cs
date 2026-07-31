using Blog.Domain.Plugins;

namespace Blog.Extensions.Sdk;

/// <summary>
/// Entry point for third-party extensions. Prefer implementing <see cref="IBlogPlugin"/>
/// and shipping a DLL under ContentRoot/plugins.
/// </summary>
public interface IBlogExtension : IBlogPlugin
{
    /// <summary>Optional DI registration before InitializeAsync.</summary>
    void ConfigureServices(IServiceCollection services) { }
}

// Re-export for extension authors
public static class ExtensionSdk
{
    public const string PluginsFolder = "plugins";
    public const string WidgetsZoneSidebar = "sidebar";
    public const string WidgetsZoneFooter = "footer";
    public const string WidgetsZonePostBottom = "post-bottom";
    public const string WidgetsZoneHomeHero = "home-hero";
    public const string WidgetsZoneAdminDashboard = "admin-dashboard";

    public const string PipelineSlotEarly = "early";
    public const string PipelineSlotPreAuth = "pre-auth";
    public const string PipelineSlotPostAuth = "post-auth";
    public const string PipelineSlotPreEndpoint = "pre-endpoint";
}
