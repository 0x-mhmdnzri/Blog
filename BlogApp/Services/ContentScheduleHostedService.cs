using BlogApp.Data;
using BlogApp.Developer.Domain;
using BlogApp.Developer.Messaging;
using BlogApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Services;

/// <summary>
/// BACKGROUND schedule/expire — does not depend on someone opening admin or a post page.
/// FEATURES.md Content Management: Scheduled Publishing + Content Expiration workers.
/// </summary>
public sealed class ContentScheduleHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ContentScheduleHostedService> _log;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);

    public ContentScheduleHostedService(IServiceScopeFactory scopes, ILogger<ContentScheduleHostedService> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("ContentScheduleHostedService started (interval={Sec}s)", _interval.TotalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "ContentSchedule tick failed");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var events = scope.ServiceProvider.GetService<IDomainEventPublisher>();
        var now = DateTime.UtcNow;

        // Tracking required for updates
        var toPublish = await db.Posts
            .AsTracking()
            .Where(p => !p.IsDeleted && !p.IsPublished
                        && p.ScheduledPublishAtUtc != null
                        && p.ScheduledPublishAtUtc <= now)
            .ToListAsync(ct);

        foreach (var p in toPublish)
        {
            p.IsPublished = true;
            p.PublishedAtUtc ??= now;
            p.ScheduledPublishAtUtc = null;
            p.UpdatedAtUtc = now;
        }

        var toExpire = await db.Posts
            .AsTracking()
            .Where(p => !p.IsDeleted && p.IsPublished
                        && p.ExpiresAtUtc != null
                        && p.ExpiresAtUtc <= now)
            .ToListAsync(ct);

        foreach (var p in toExpire)
        {
            p.IsPublished = false;
            p.UpdatedAtUtc = now;
        }

        if (toPublish.Count + toExpire.Count == 0)
            return;

        await db.SaveChangesAsync(ct);

        _log.LogInformation("ContentSchedule published={Pub} expired={Exp}", toPublish.Count, toExpire.Count);

        if (events is null) return;

        foreach (var p in toPublish)
        {
            try
            {
                await events.PublishAsync(new PostPublishedDomainEvent(
                    p.Id, p.Title, p.Slug, p.AuthorId, p.PublishedAtUtc ?? now), ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "EDD publish failed for scheduled PostId={Id}", p.Id);
            }
        }

        foreach (var p in toExpire)
        {
            try
            {
                await events.PublishAsync(new PostUnpublishedDomainEvent(p.Id, p.Slug), ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "EDD unpublish failed for expired PostId={Id}", p.Id);
            }
        }
    }
}
