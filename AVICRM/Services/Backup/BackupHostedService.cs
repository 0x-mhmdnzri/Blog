using Microsoft.Extensions.Options;

namespace AVICRM.Services.Backup;

/// <summary>
/// Periodic full backups to the Docker data volume. Interval ≈ target RPO.
/// </summary>
public sealed class BackupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<BackupOptions> _options;
    private readonly ILogger<BackupHostedService> _log;

    public BackupHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<BackupOptions> options,
        ILogger<BackupHostedService> log)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Delay first run so the app finishes warming up / migrations
        try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            var opts = _options.Value;
            if (!opts.Enabled)
            {
                try { await Task.Delay(TimeSpan.FromHours(1), stoppingToken); }
                catch (OperationCanceledException) { break; }
                continue;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var backup = scope.ServiceProvider.GetRequiredService<IAppBackupService>();
                await backup.CreateFullBackupAsync("system:scheduler", kind: "scheduled", stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Scheduled backup failed");
            }

            var hours = Math.Clamp(opts.IntervalHours, 1, 168);
            try { await Task.Delay(TimeSpan.FromHours(hours), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
