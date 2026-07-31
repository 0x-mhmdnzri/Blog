using Blog.Application.EventBus;
using Blog.Domain.Plugins;
using Blog.Infrastructure.EventBus;
using Blog.Infrastructure.Health;
using Blog.Infrastructure.Middleware;
using Blog.Infrastructure.Observability;
using Blog.Infrastructure.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Blog.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddBlogInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<InProcessDomainEventBus>();
        services.AddSingleton<IDomainEventBus>(sp => sp.GetRequiredService<InProcessDomainEventBus>());
        services.AddSingleton<IPluginHost, PluginHost>();
        services.AddSingleton<PluginLoader>();
        services.AddSingleton<PipelineExtensionRegistry>();

        services.AddBlogHealthChecks();
        services.AddBlogOpenTelemetry(config);

        return services;
    }
}
