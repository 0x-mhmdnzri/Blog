using System.ComponentModel.DataAnnotations;

namespace AVICRM.Models;

public enum BackgroundJobStatus
{
    Pending = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4
}

/// <summary>Persistent background work unit (email, image optimize, search index, generic).</summary>
public class BackgroundJob
{
    public long Id { get; set; }

    [Required, MaxLength(64)]
    public string Type { get; set; } = string.Empty;

    /// <summary>JSON payload for the worker.</summary>
    public string? Payload { get; set; }

    public BackgroundJobStatus Status { get; set; } = BackgroundJobStatus.Pending;

    public int Attempts { get; set; }
    public int MaxAttempts { get; set; } = 5;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? AvailableAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    [MaxLength(2000)]
    public string? LastError { get; set; }
}

public static class BackgroundJobTypes
{
    public const string SendEmail = "email.send";
    public const string OptimizeImage = "media.optimize";
    public const string IndexPost = "search.index_post";
    public const string RemovePostIndex = "search.remove_post";
}
