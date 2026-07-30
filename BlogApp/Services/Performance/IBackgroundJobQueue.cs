namespace BlogApp.Services.Performance;

public interface IBackgroundJobQueue
{
    Task EnqueueAsync(string type, object? payload = null, DateTime? availableAtUtc = null, int maxAttempts = 5, CancellationToken ct = default);
    Task EnqueueEmailAsync(string to, string subject, string body, bool isHtml = true, CancellationToken ct = default);
    Task EnqueueImageOptimizeAsync(int mediaId, CancellationToken ct = default);
    Task EnqueueIndexPostAsync(int postId, CancellationToken ct = default);
    Task EnqueueRemovePostIndexAsync(int postId, CancellationToken ct = default);
}
