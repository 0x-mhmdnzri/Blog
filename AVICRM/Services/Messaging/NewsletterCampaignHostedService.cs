using AVICRM.Data;
using AVICRM.Models;
using Microsoft.EntityFrameworkCore;

namespace AVICRM.Services.Messaging;

/// <summary>Picks up Scheduled newsletter campaigns whose ScheduledAtUtc has passed.</summary>
public sealed class NewsletterCampaignHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<NewsletterCampaignHostedService> _logger;

    public NewsletterCampaignHostedService(
        IServiceScopeFactory scopes,
        ILogger<NewsletterCampaignHostedService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Newsletter campaign scheduler tick failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var nl = scope.ServiceProvider.GetRequiredService<INewsletterService>();

        var now = DateTime.UtcNow;
        var due = await db.NewsletterCampaigns
            .Where(c => c.Status == NewsletterCampaignStatus.Scheduled
                        && c.ScheduledAtUtc != null
                        && c.ScheduledAtUtc <= now)
            .OrderBy(c => c.ScheduledAtUtc)
            .Select(c => c.Id)
            .Take(5)
            .ToListAsync(ct);

        foreach (var id in due)
        {
            try
            {
                await nl.SendCampaignAsync(id, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled newsletter campaign {Id} failed", id);
            }
        }
    }
}
