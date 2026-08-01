using BlogApp.Data;
using BlogApp.Developer.Messaging;
using BlogApp.Developer.Middleware;
using BlogApp.Developer.Observability;
using BlogApp.Developer.Plugins;
using BlogApp.Developer.Widgets;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace BlogApp.Developer;

/// <summary>
/// Single-host (monolith) composition root for FEATURES.md Developer Features:
/// Event Bus (MassTransit), Domain Events, Plugins, Widgets, Middleware slots,
/// Health Checks, Metrics, OpenTelemetry, Theme system (via IThemeService).
/// </summary>
public static class DeveloperFeaturesBootstrap
{
    public static IServiceCollection AddDeveloperFeatures(this IServiceCollection services, IConfiguration config)
    {
        // ── MassTransit EDD ─────────────────────────────────────────────────
        var rabbitHost = config["RabbitMq:HostName"];
        var useRabbit = !string.IsNullOrWhiteSpace(rabbitHost);

        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            x.AddConsumer<PostPublishedConsumer>();
            x.AddConsumer<PostCreatedConsumer>();
            x.AddConsumer<CommentApprovedConsumer>();
            x.AddConsumer<AuthorFollowedConsumer>();
            x.AddConsumer<BlogApp.Services.Messaging.NotifyOnPostPublishedConsumer>();
            x.AddConsumer<BlogApp.Services.Messaging.NotifyOnCommentApprovedConsumer>();
            x.AddConsumer<BlogApp.Services.Messaging.NotifyOnAuthorFollowedConsumer>();
            x.AddConsumer<BlogApp.Services.Messaging.WebhookDispatchConsumer>();

            if (useRabbit)
            {
                x.UsingRabbitMq((ctx, cfg) =>
                {
                    var port = config.GetValue("RabbitMq:Port", 5672);
                    var user = config["RabbitMq:UserName"] ?? "guest";
                    var pass = config["RabbitMq:Password"] ?? "guest";
                    var vhost = config["RabbitMq:VirtualHost"] ?? "/";

                    cfg.Host(rabbitHost, (ushort)port, vhost, h =>
                    {
                        h.Username(user);
                        h.Password(pass);
                    });

                    cfg.ConfigureEndpoints(ctx);
                });
            }
            else
            {
                x.UsingInMemory((ctx, cfg) =>
                {
                    cfg.ConfigureEndpoints(ctx);
                });
            }
        });

        services.AddScoped<IDomainEventPublisher, MassTransitDomainEventPublisher>();

        // ── Plugins / widgets / pipeline ────────────────────────────────────
        services.AddSingleton<PluginLoader>();
        services.AddSingleton<PipelineExtensionRegistry>();
        services.AddSingleton<WidgetRegistry>(sp =>
        {
            var reg = new WidgetRegistry();
            reg.Register(new PopularPostsWidget());
            reg.Register(new RecentPostsWidget());
            return reg;
        });

        // ── Health ──────────────────────────────────────────────────────────
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy("BlogApp"), tags: new[] { "live" })
            .AddCheck<SqliteHealthCheck>("sqlite", tags: new[] { "ready" });

        // ── OpenTelemetry ───────────────────────────────────────────────────
        var serviceName = config["OpenTelemetry:ServiceName"] ?? "BlogApp";
        var otlp = config["OpenTelemetry:OtlpEndpoint"];
        var prometheus = config.GetValue("OpenTelemetry:Prometheus", true);

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(t =>
            {
                t.AddAspNetCoreInstrumentation()
                 .AddHttpClientInstrumentation()
                 .AddSource("BlogApp");
                if (!string.IsNullOrWhiteSpace(otlp))
                    t.AddOtlpExporter(o => o.Endpoint = new Uri(otlp));
            })
            .WithMetrics(m =>
            {
                m.AddAspNetCoreInstrumentation()
                 .AddHttpClientInstrumentation()
                 .AddMeter(BlogMetrics.Meter.Name);
                if (prometheus)
                    m.AddPrometheusExporter();
                if (!string.IsNullOrWhiteSpace(otlp))
                    m.AddOtlpExporter(o => o.Endpoint = new Uri(otlp));
            });

        return services;
    }

    public static async Task UseDeveloperFeaturesAsync(this WebApplication app)
    {
        var pluginsDir = Path.Combine(app.Environment.ContentRootPath, "plugins");
        var loader = app.Services.GetRequiredService<PluginLoader>();
        // Plugins that need ConfigureServices must load before Build;
        // here we only StartAsync for already-registered plugins.
        await loader.StartAllAsync(app.Services);

        var transport = string.IsNullOrWhiteSpace(app.Configuration["RabbitMq:HostName"])
            ? "InMemory"
            : "RabbitMQ";
        app.Logger.LogInformation("MassTransit transport={Transport}; plugins dir={Dir}", transport, pluginsDir);
    }

    public static void MapDeveloperEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/healthz", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        }).AllowAnonymous().DisableRateLimiting();

        app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("ready")
        }).AllowAnonymous().DisableRateLimiting();

        if (app.Configuration.GetValue("OpenTelemetry:Prometheus", true))
            app.MapPrometheusScrapingEndpoint("/metrics").AllowAnonymous().DisableRateLimiting();

        app.MapGet("/widgets/{zone}", async (string zone, WidgetRegistry registry, IServiceProvider sp, CancellationToken ct) =>
        {
            var parts = new List<string>();
            foreach (var w in registry.ForZone(zone))
                parts.Add(await w.RenderHtmlAsync(sp, ct));
            return Results.Content(string.Join("\n", parts), "text/html; charset=utf-8");
        }).AllowAnonymous();

        app.MapGet("/dev/plugins", (PluginLoader loader) =>
        {
            var data = loader.Loaded.Select(p => new { p.Id, p.Name, p.Version });
            return Results.Json(data);
        }).RequireAuthorization(policy => policy.RequireRole("SuperAdmin"));

        app.MapGet("/dev/bus", (IConfiguration config) =>
        {
            var host = config["RabbitMq:HostName"];
            return Results.Json(new
            {
                transport = string.IsNullOrWhiteSpace(host) ? "InMemory" : "RabbitMQ",
                host = host ?? "",
                massTransit = true,
                edd = true
            });
        }).RequireAuthorization(policy => policy.RequireRole("SuperAdmin"));
    }
}

/// <summary>Readiness: SQLite can connect.</summary>
file sealed class SqliteHealthCheck : IHealthCheck
{
    private readonly ApplicationDbContext _db;

    public SqliteHealthCheck(ApplicationDbContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("sqlite ok")
                : HealthCheckResult.Unhealthy("sqlite unreachable");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("sqlite error", ex);
        }
    }
}
