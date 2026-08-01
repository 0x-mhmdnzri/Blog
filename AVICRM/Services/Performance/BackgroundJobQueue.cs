using System.Text.Json;
using AVICRM.Data;
using AVICRM.Models;
using Microsoft.EntityFrameworkCore;

namespace AVICRM.Services.Performance;

public sealed class BackgroundJobQueue : IBackgroundJobQueue
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly ApplicationDbContext _db;

    public BackgroundJobQueue(ApplicationDbContext db) => _db = db;

    public async Task EnqueueAsync(string type, object? payload = null, DateTime? availableAtUtc = null, int maxAttempts = 5, CancellationToken ct = default)
    {
        var job = new BackgroundJob
        {
            Type = type,
            Payload = payload is null ? null : JsonSerializer.Serialize(payload, JsonOpts),
            Status = BackgroundJobStatus.Pending,
            MaxAttempts = maxAttempts,
            AvailableAtUtc = availableAtUtc,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.BackgroundJobs.Add(job);
        await _db.SaveChangesAsync(ct);
    }

    public Task EnqueueEmailAsync(string to, string subject, string body, bool isHtml = true, CancellationToken ct = default) =>
        EnqueueAsync(BackgroundJobTypes.SendEmail, new EmailJobPayload(to, subject, body, isHtml), maxAttempts: 8, ct: ct);

    public Task EnqueueImageOptimizeAsync(int mediaId, CancellationToken ct = default) =>
        EnqueueAsync(BackgroundJobTypes.OptimizeImage, new MediaJobPayload(mediaId), ct: ct);

    public Task EnqueueIndexPostAsync(int postId, CancellationToken ct = default) =>
        EnqueueAsync(BackgroundJobTypes.IndexPost, new PostJobPayload(postId), ct: ct);

    public Task EnqueueRemovePostIndexAsync(int postId, CancellationToken ct = default) =>
        EnqueueAsync(BackgroundJobTypes.RemovePostIndex, new PostJobPayload(postId), ct: ct);
}

public record EmailJobPayload(string To, string Subject, string Body, bool IsHtml);
public record MediaJobPayload(int MediaId);
public record PostJobPayload(int PostId);
