namespace Blog.Core.DomainService;

using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        // Register pure domain services here (Artix: OTP, XPRules, TierCalculator).
        return services;
    }
}
