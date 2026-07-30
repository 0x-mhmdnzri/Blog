using System.Text.Json;
using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BlogApp.Services.Performance;

/// <summary>Polls BackgroundJobs table and dispatches typed handlers.</summary>
public sealed class BackgroundJobWorker : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BackgroundJobsOptions _opt;
    private readonly ILogger<BackgroundJobWorker> _logger;

    public BackgroundJobWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<PerformanceOptions> opt,
        ILogger<BackgroundJobWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _opt = opt.Value.Jobs;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opt.Enabled)
        {
            _logger.LogInformation("Background jobs disabled");
            return;
        }

        _logger.LogInformation("Background job worker started Poll={Ms}ms Batch={Batch}",
            _opt.PollIntervalMs, _opt.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessBatchAsync(stoppingToken);
                if (processed == 0)
                    await Task.Delay(_opt.PollIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background job worker loop error");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;

        var jobs = await db.BackgroundJobs
            .AsTracking()
            .Where(j => j.Status == BackgroundJobStatus.Pending
                        && (j.AvailableAtUtc == null || j.AvailableAtUtc <= now)
                        && j.Attempts < j.MaxAttempts)
            .OrderBy(j => j.Id)
            .Take(_opt.BatchSize)
            .ToListAsync(ct);

        if (jobs.Count == 0) return 0;

        foreach (var job in jobs)
        {
            job.Status = BackgroundJobStatus.Running;
            job.StartedAtUtc = DateTime.UtcNow;
            job.Attempts++;
        }
        await db.SaveChangesAsync(ct);

        var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var images = scope.ServiceProvider.GetRequiredService<ImageOptimizeService>();
        var search = scope.ServiceProvider.GetRequiredService<SearchIndexService>();

        foreach (var job in jobs)
        {
            try
            {
                await DispatchAsync(job, email, images, search, ct);
                job.Status = BackgroundJobStatus.Succeeded;
                job.CompletedAtUtc = DateTime.UtcNow;
                job.LastError = null;
            }
            catch (Exception ex)
            {
                job.LastError = ex.Message.Length > 1900 ? ex.Message[..1900] : ex.Message;
                if (job.Attempts >= job.MaxAttempts)
                {
                    job.Status = BackgroundJobStatus.Failed;
                    job.CompletedAtUtc = DateTime.UtcNow;
                }
                else
                {
                    job.Status = BackgroundJobStatus.Pending;
                    job.AvailableAtUtc = DateTime.UtcNow.AddSeconds(Math.Pow(2, job.Attempts) * 5);
                }
                _logger.LogWarning(ex, "Job failed Id={Id} Type={Type} Attempt={Attempt}",
                    job.Id, job.Type, job.Attempts);
            }
        }

        await db.SaveChangesAsync(ct);
        return jobs.Count;
    }

    private static async Task DispatchAsync(
        BackgroundJob job,
        IEmailSender email,
        ImageOptimizeService images,
        SearchIndexService search,
        CancellationToken ct)
    {
        switch (job.Type)
        {
            case BackgroundJobTypes.SendEmail:
            {
                var p = JsonSerializer.Deserialize<EmailJobPayload>(job.Payload ?? "{}", JsonOpts)
                        ?? throw new InvalidOperationException("Invalid email payload");
                await email.SendAsync(p.To, p.Subject, p.Body, p.IsHtml, ct);
                break;
            }
            case BackgroundJobTypes.OptimizeImage:
            {
                var p = JsonSerializer.Deserialize<MediaJobPayload>(job.Payload ?? "{}", JsonOpts)
                        ?? throw new InvalidOperationException("Invalid media payload");
                await images.OptimizeAsync(p.MediaId, ct);
                break;
            }
            case BackgroundJobTypes.IndexPost:
            {
                var p = JsonSerializer.Deserialize<PostJobPayload>(job.Payload ?? "{}", JsonOpts)
                        ?? throw new InvalidOperationException("Invalid post payload");
                await search.IndexPostAsync(p.PostId, ct);
                break;
            }
            case BackgroundJobTypes.RemovePostIndex:
            {
                var p = JsonSerializer.Deserialize<PostJobPayload>(job.Payload ?? "{}", JsonOpts)
                        ?? throw new InvalidOperationException("Invalid post payload");
                await search.RemovePostAsync(p.PostId, ct);
                break;
            }
            default:
                throw new InvalidOperationException($"Unknown job type: {job.Type}");
        }
    }
}
