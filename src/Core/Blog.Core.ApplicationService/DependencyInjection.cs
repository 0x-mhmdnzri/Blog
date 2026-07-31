namespace Blog.Core.ApplicationService;

using Features.Posts.Admin.Commands.PublishPost;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<PublishPostCommandHandler>();
        return services;
    }
}
