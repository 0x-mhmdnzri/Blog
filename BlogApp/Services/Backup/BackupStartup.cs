using BlogApp.Services.Enterprise;
using BlogApp.Services.Seo;

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

        // Per-post Open Graph share cards (GitHub-style banners)
        services.AddSingleton<IPostOgCardService, PostOgCardService>();

        return services;
    }
}
