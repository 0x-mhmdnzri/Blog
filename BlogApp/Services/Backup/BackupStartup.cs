using BlogApp.Services.Enterprise;

namespace BlogApp.Services.Backup;

public static class BackupStartup
{
    public static IServiceCollection AddAppBackup(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BackupOptions>(configuration.GetSection(BackupOptions.Section));
        services.AddScoped<IAppBackupService, AppBackupService>();
        services.AddHostedService<BackupHostedService>();

        // Enterprise admin panel (tenants, SSO, GDPR, backup orchestration)
        services.AddScoped<IEnterpriseService, EnterpriseService>();

        return services;
    }
}
