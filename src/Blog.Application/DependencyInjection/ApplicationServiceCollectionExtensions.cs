using Blog.Application.EventBus;
using Blog.Application.Plugins;
using Blog.Application.Widgets;
using Microsoft.Extensions.DependencyInjection;

namespace Blog.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddBlogApplication(this IServiceCollection services)
    {
        services.AddSingleton<IWidgetRegistry, WidgetRegistry>();
        services.AddSingleton<IPluginRegistry, PluginRegistry>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        return services;
    }
}
