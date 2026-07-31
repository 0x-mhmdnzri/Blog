namespace Blog.Infra.Messaging;

using Blog.Core.Contract.Primitives.Messaging;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddMessagingInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<InProcessEventPublisher>();
        services.AddSingleton<IEventPublisher>(sp => sp.GetRequiredService<InProcessEventPublisher>());
        return services;
    }
}
