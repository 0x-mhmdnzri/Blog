using BlogApp.Data;
using BlogApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Services.Seo;

/// <summary>
/// Drains <see cref="BotCrawlLogQueue"/> in batches and purges rows older than retention.
/// </summary>
public sealed class BotCrawlLogHostedService : BackgroundService
{
    public const int RetentionDays = 90;

    private readonly BotCrawlLogQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<BotCrawlLogHostedService> _log;

    public BotCrawlLogHostedService(
        BotCrawlLogQueue queue,
        IServiceScopeFactory scopes,
        ILogger<BotCrawlLogHostedService> log)
    {
        _queue = queue;
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("BotCrawlLogHostedService started (retention={Days}d)", RetentionDays);

        var batch = new List<BotCrawlHit>(128);
        var lastPurge = DateTime.UtcNow.AddHours(-1);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                batch.Clear();

                // Wait for at least one item
                var first = await _queue.Reader.ReadAsync(stoppingToken);
                batch.Add(first);

                // Drain quickly up to batch size or 200ms
                var deadline = DateTime.UtcNow.AddMilliseconds(200);
                while (batch.Count < 100 && DateTime.UtcNow < deadline)
                {
                    if (_queue.Reader.TryRead(out var more))
                        batch.Add(more);
                    else
                        await Task.Delay(15, stoppingToken);
                }

                await FlushAsync(batch, stoppingToken);

                if (DateTime.UtcNow - lastPurge > TimeSpan.FromHours(6))
                {
                    await PurgeAsync(stoppingToken);
                    lastPurge = DateTime.UtcNow;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "BotCrawlLog flush failed");
                try { await Task.Delay(2000, stoppingToken); } catch { /* ignore */ }
            }
        }

        // Final drain
        try
        {
            batch.Clear();
            while (_queue.Reader.TryRead(out var hit))
                batch.Add(hit);
            if (batch.Count > 0)
                await FlushAsync(batch, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "BotCrawlLog final drain failed");
        }
    }

    private async Task FlushAsync(List<BotCrawlHit> batch, CancellationToken ct)
    {
        if (batch.Count == 0) return;

        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Tracking required for inserts
        db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        db.BotCrawlHits.AddRange(batch);
        await db.SaveChangesAsync(ct);
    }

    private async Task PurgeAsync(CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // SQLite-friendly chunked delete
        var deleted = await db.BotCrawlHits
            .Where(h => h.HitAtUtc < cutoff)
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
            _log.LogInformation("BotCrawlLog purged {Count} rows older than {Cutoff:u}", deleted, cutoff);
    }
}
