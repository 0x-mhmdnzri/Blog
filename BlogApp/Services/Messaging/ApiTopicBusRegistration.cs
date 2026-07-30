using Microsoft.Extensions.DependencyInjection;

namespace BlogApp.Services.Messaging;

public static class ApiTopicBusRegistration
{
    public static IServiceCollection AddApiTopicBus(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<ApiTopicBusOptions>(config.GetSection("ApiTopicBus"));
        services.AddSingleton<ApiTopicBus>();
        services.AddSingleton<IApiTopicBus>(sp => sp.GetRequiredService<ApiTopicBus>());
        services.AddHostedService(sp => sp.GetRequiredService<ApiTopicBus>());

        services.AddScoped<IApiWorkHandler, CommentCreateWorkHandler>();
        services.AddScoped<IApiWorkHandler, PostsListWorkHandler>();
        services.AddScoped<ApiWorkHandlerRegistry>();

        return services;
    }
}
