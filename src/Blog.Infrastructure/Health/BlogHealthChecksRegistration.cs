using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Blog.Infrastructure.Health;

public static class BlogHealthChecksRegistration
{
    public static IHealthChecksBuilder AddBlogHealthChecks(this IServiceCollection services)
    {
        return services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy("BlogApp process is running"), tags: new[] { "live" })
            .AddCheck<DomainEventBusHealthCheck>("domain_event_bus", tags: new[] { "ready" })
            .AddCheck<PluginRegistryHealthCheck>("plugins", tags: new[] { "ready" });
    }
}

public sealed class DomainEventBusHealthCheck : IHealthCheck
{
    private readonly Application.EventBus.IDomainEventBus _bus;

    public DomainEventBusHealthCheck(Application.EventBus.IDomainEventBus bus) => _bus = bus;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_bus is not null
            ? HealthCheckResult.Healthy("Domain event bus registered")
            : HealthCheckResult.Unhealthy("Domain event bus missing"));
    }
}

public sealed class PluginRegistryHealthCheck : IHealthCheck
{
    private readonly Application.Plugins.IPluginRegistry _registry;

    public PluginRegistryHealthCheck(Application.Plugins.IPluginRegistry registry) => _registry = registry;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var count = _registry.Plugins.Count;
        return Task.FromResult(HealthCheckResult.Healthy($"{count} plugin(s) registered"));
    }
}
