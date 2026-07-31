using Blog.Core.ApplicationService;
using Blog.Core.DomainService;
using Blog.Infra.Messaging;
using Blog.Infra.Observability;
using Blog.Infra.Plugins;

namespace BlogApp.Developer;

/// <summary>
/// Host composition root for Developer Features (Artix.API layer layout).
/// </summary>
public static class DeveloperFeaturesBootstrap
{
    public static IServiceCollection AddDeveloperFeatures(this IServiceCollection services, IConfiguration config)
    {
        services.AddApplicationServices();
        services.AddDomainServices();
        services.AddMessagingInfrastructure();
        services.AddObservabilityInfrastructure(config);
        services.AddPluginInfrastructure();
        return services;
    }

    public static Task UseDeveloperFeaturesAsync(this WebApplication app)
    {
        var pluginsDir = Path.Combine(app.Environment.ContentRootPath, "plugins");
        var loader = app.Services.GetRequiredService<PluginLoader>();
        // ConfigureServices already ran; load metadata only at runtime for listing.
        // Full ConfigureServices must happen before Build — call Load early in Program if needed.
        if (Directory.Exists(pluginsDir))
        {
            // Runtime discovery for /dev/plugins listing
            _ = loader;
        }

        return Task.CompletedTask;
    }

    public static void MapDeveloperEndpoints(this WebApplication app)
    {
        app.MapObservabilityEndpoints();

        app.MapGet("/dev/plugins", (PluginLoader loader) =>
        {
            var data = loader.Loaded.Select(p => new { p.Id, p.Name, p.Version });
            return Results.Json(data);
        }).RequireAuthorization(policy => policy.RequireRole("SuperAdmin"));
    }
}
