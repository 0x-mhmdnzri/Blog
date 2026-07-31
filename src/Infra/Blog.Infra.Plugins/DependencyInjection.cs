namespace Blog.Infra.Plugins;

using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddPluginInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<PluginLoader>();
        return services;
    }
}
