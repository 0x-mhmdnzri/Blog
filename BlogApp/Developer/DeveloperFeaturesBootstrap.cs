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
        services.AddSingleton<WidgetRegistry>();

        // ── Health ──────────────────────────────────────────────────────────
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy("ok"));

        // ── OpenTelemetry (optional OTLP) ───────────────────────────────────
        var otlp = config["OpenTelemetry:OtlpEndpoint"];
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("BlogApp"))
            .WithTracing(t =>
            {
                t.AddAspNetCoreInstrumentation();
                t.AddHttpClientInstrumentation();
                if (!string.IsNullOrWhiteSpace(otlp))
                    t.AddOtlpExporter(o => o.Endpoint = new Uri(otlp));
            })
            .WithMetrics(m =>
            {
                m.AddAspNetCoreInstrumentation();
                m.AddHttpClientInstrumentation();
                m.AddRuntimeInstrumentation();
                m.AddPrometheusExporter();
                if (!string.IsNullOrWhiteSpace(otlp))
                    m.AddOtlpExporter(o => o.Endpoint = new Uri(otlp));
            });

        return services;
    }

    public static WebApplication UseDeveloperFeatures(this WebApplication app)
    {
        var transport = string.IsNullOrWhiteSpace(app.Configuration["RabbitMq:HostName"]) ? "InMemory" : "RabbitMQ";
        var pluginsDir = Path.Combine(app.Environment.ContentRootPath, "plugins");
        app.Logger.LogInformation("MassTransit transport={Transport}; plugins dir={Dir}", transport, pluginsDir);

        app.MapHealthChecks("/health");
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = _ => true
        });

        return app;
    }
}
