using System.ComponentModel.DataAnnotations;

namespace BlogApp.Models;

public enum NotificationKind
{
    System = 0,
    NewComment = 1,
    CommentApproved = 2,
    NewFollower = 3,
    WeeklyDigest = 4,
    AdCampaign = 5,
    Broadcast = 6,
    NewPost = 7,
    AdminMessage = 8,
    CategoryBatch = 9,
    Mention = 10
}

/// <summary>Who should receive a composed notification.</summary>
public enum NotificationAudience
{
    /// <summary>One user by id.</summary>
    SingleUser = 0,
    /// <summary>Every authenticated role (all users).</summary>
    Broadcast = 1,
    /// <summary>All Authors + SuperAdmins.</summary>
    AllAuthors = 2,
    /// <summary>Followers of a specific author.</summary>
    AuthorFollowers = 3,
    /// <summary>Users who interact with posts in a category.</summary>
    CategoryReaders = 4,
    /// <summary>Explicit list of user ids (batch).</summary>
    UserList = 5
}

/// <summary>In-app notification row (bell dropdown).</summary>
public class AppNotification
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    public NotificationKind Kind { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Body { get; set; }

    [MaxLength(500)]
    public string? LinkUrl { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Per-user notification channel preferences.</summary>
public class UserNotificationPrefs
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    public bool InAppEnabled { get; set; } = true;
    public bool EmailEnabled { get; set; } = true;
    public bool SmsEnabled { get; set; }
    public bool PushEnabled { get; set; } = true;

    public bool NotifyNewComment { get; set; } = true;
    public bool NotifyNewFollower { get; set; } = true;
    public bool NotifyNewPostFromFollowed { get; set; } = true;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Queued outbound notification (admin compose / campaigns).</summary>
public class NotificationOutbox
{
    public int Id { get; set; }

    public NotificationAudience Audience { get; set; }

    [MaxLength(450)]
    public string? TargetUserId { get; set; }

    [MaxLength(2000)]
    public string? TargetUserIdsJson { get; set; }

    public int? AuthorId { get; set; }
    public int? CategoryId { get; set; }

    public NotificationKind Kind { get; set; } = NotificationKind.AdminMessage;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Body { get; set; }

    [MaxLength(500)]
    public string? LinkUrl { get; set; }

    public bool SendInApp { get; set; } = true;
    public bool SendEmail { get; set; }
    public bool SendPush { get; set; }
    public bool SendSms { get; set; }

    public DateTime? ScheduledAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }

    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Web Push subscription (browser endpoint).</summary>
public class PushSubscription
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string Endpoint { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? P256dh { get; set; }

    [MaxLength(200)]
    public string? Auth { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Realtime event published on the notification bus (SSE / SignalR).</summary>
public record NotificationDeliveredEvent(
    int NotificationId,
    string UserId,
    NotificationKind Kind,
    string Title,
    string? Body,
    string? LinkUrl,
    DateTime CreatedAtUtc);
