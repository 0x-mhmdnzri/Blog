using Blog.Application.DependencyInjection;
using Blog.Application.EventBus;
using Blog.Application.Plugins;
using Blog.Application.Widgets;
using Blog.Domain.Aggregates.Posts.Events;
using Blog.Domain.Widgets;
using Blog.Infrastructure.DependencyInjection;
using Blog.Infrastructure.EventHandlers;
using Blog.Infrastructure.Middleware;
using Blog.Infrastructure.Plugins;
using Blog.Infrastructure.Widgets;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BlogApp.Developer;

/// <summary>
/// Host wiring for FEATURES.md → Developer Features (clean architecture layers).
/// </summary>
public static class DeveloperFeaturesBootstrap
{
    public static IServiceCollection AddDeveloperFeatures(this IServiceCollection services, IConfiguration config)
    {
        services.AddBlogApplication();
        services.AddBlogInfrastructure(config);

        // Domain event handlers
        services.AddScoped<IDomainEventHandler<PostPublishedDomainEvent>, PostPublishedLoggingHandler>();
        services.AddScoped<IDomainEventHandler<PostCreatedDomainEvent>, PostCreatedLoggingHandler>();

        // Built-in widgets
        services.AddSingleton<IWidgetDescriptor, ReadingTipsWidget>();
        services.AddSingleton<IWidgetDescriptor, HealthStatusWidget>();

        return services;
    }

    public static async Task UseDeveloperFeaturesAsync(this WebApplication app)
    {
        // Register built-in widgets into registry
        var widgets = app.Services.GetRequiredService<IWidgetRegistry>();
        foreach (var w in app.Services.GetServices<IWidgetDescriptor>())
            widgets.Register(w);

        // Load external plugins from ContentRoot/plugins
        var loader = app.Services.GetRequiredService<PluginLoader>();
        loader.LoadFromDirectory();

        var plugins = app.Services.GetRequiredService<IPluginRegistry>();
        await plugins.InitializeAllAsync();

        // Extension middleware slots (plugins can register into PipelineExtensionRegistry)
        app.UseBlogExtensionSlot("early");
    }

    public static void MapDeveloperEndpoints(this WebApplication app)
    {
        // Rich health checks (live / ready)
        app.MapHealthChecks("/healthz", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live"),
            ResponseWriter = WriteHealthJson
        }).AllowAnonymous();

        app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("ready") || r.Tags.Contains("live"),
            ResponseWriter = WriteHealthJson
        }).AllowAnonymous();

        // Prometheus metrics (OpenTelemetry exporter)
        app.MapPrometheusScrapingEndpoint("/metrics");

        // Widget zone render (HTML fragment)
        app.MapGet("/widgets/{zone}", async (string zone, IWidgetRegistry registry, IServiceProvider sp, HttpContext ctx) =>
        {
            var list = registry.GetForZone(zone);
            if (list.Count == 0)
                return Results.Content(string.Empty, "text/html; charset=utf-8");

            var parts = new List<string>();
            var renderCtx = new WidgetRenderContext
            {
                Zone = zone,
                Culture = ctx.Request.Headers.AcceptLanguage.FirstOrDefault(),
                UserId = ctx.User.Identity?.IsAuthenticated == true ? ctx.User.Identity.Name : null,
                Services = sp
            };
            foreach (var w in list)
            {
                var result = await w.RenderAsync(renderCtx, ctx.RequestAborted);
                if (!string.IsNullOrEmpty(result.Html))
                    parts.Add(result.Html);
            }

            return Results.Content(string.Join('\n', parts), "text/html; charset=utf-8");
        }).AllowAnonymous();

        // Developer introspection (SuperAdmin)
        app.MapGet("/dev/plugins", (IPluginRegistry registry) =>
        {
            var data = registry.Plugins.Select(p => new
            {
                p.Id,
                p.Name,
                p.Version,
                p.Description
            });
            return Results.Json(data);
        }).RequireAuthorization(policy => policy.RequireRole("SuperAdmin"));

        app.MapGet("/dev/widgets", (IWidgetRegistry registry) =>
        {
            var data = registry.All.Select(w => new
            {
                w.Id,
                w.DisplayName,
                Zones = w.Zones,
                w.Order
            });
            return Results.Json(data);
        }).RequireAuthorization(policy => policy.RequireRole("SuperAdmin"));
    }

    private static async Task WriteHealthJson(HttpContext ctx, HealthReport report)
    {
        ctx.Response.ContentType = "application/json; charset=utf-8";
        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            entries = report.Entries.ToDictionary(
                e => e.Key,
                e => new
                {
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    durationMs = e.Value.Duration.TotalMilliseconds
                })
        };
        await ctx.Response.WriteAsJsonAsync(payload);
    }
}
