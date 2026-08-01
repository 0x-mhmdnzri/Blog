namespace AVICRM.Services.Enterprise;

public static class EnterpriseStartup
{
    public static IServiceCollection AddEnterpriseServices(this IServiceCollection services)
    {
        services.AddScoped<IEnterpriseService, EnterpriseService>();
        return services;
    }
}
