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
    CategoryBatch = 9
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
    public ApplicationUser User { get; set; } = null!;

    public NotificationKind Kind { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Body { get; set; }

    /// <summary>Relative path e.g. /post/slug or /Admin/Comments</summary>
    [MaxLength(400)]
    public string? LinkUrl { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Optional campaign that produced this row.</summary>
    public int? CampaignId { get; set; }
}

/// <summary>Admin/author composed notification job (immediate or scheduled).</summary>
public class NotificationCampaign
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Body { get; set; }

    [MaxLength(400)]
    public string? LinkUrl { get; set; }

    public NotificationKind Kind { get; set; } = NotificationKind.AdminMessage;

    public NotificationAudience Audience { get; set; }

    /// <summary>Target user id when Audience = SingleUser.</summary>
    [MaxLength(450)]
    public string? TargetUserId { get; set; }

    /// <summary>Author whose followers receive when Audience = AuthorFollowers.</summary>
    [MaxLength(450)]
    public string? AuthorUserId { get; set; }

    /// <summary>Category filter when Audience = CategoryReaders.</summary>
    public int? CategoryId { get; set; }

    /// <summary>Comma-separated user ids when Audience = UserList.</summary>
    [MaxLength(4000)]
    public string? TargetUserIdsCsv { get; set; }

    [Required, MaxLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Null = send immediately; otherwise wait until this UTC time.</summary>
    public DateTime? ScheduledAtUtc { get; set; }

    public bool IsSent { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public int RecipientCount { get; set; }
}

/// <summary>Per-user channel toggles (email / in-app / push / SMS for ads).</summary>
public class NotificationPreference
{
    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public bool EmailEnabled { get; set; } = true;
    public bool InAppEnabled { get; set; } = true;
    public bool PushEnabled { get; set; } = false;
    public bool SmsEnabled { get; set; } = false;

    public bool NotifyNewComment { get; set; } = true;
    public bool NotifyNewFollower { get; set; } = true;
    public bool WeeklyDigest { get; set; } = true;
    public bool NotifyNewPostFromFollowed { get; set; } = true;

    [MaxLength(32)]
    public string? PhoneE164 { get; set; }
}

/// <summary>Reader follows an author (triggers NewFollower alerts).</summary>
public class AuthorFollow
{
    [Required, MaxLength(450)]
    public string FollowerUserId { get; set; } = string.Empty;
    public ApplicationUser Follower { get; set; } = null!;

    [Required, MaxLength(450)]
    public string AuthorUserId { get; set; } = string.Empty;
    public ApplicationUser Author { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Queued outbound message (email/SMS).</summary>
public class OutboundMessage
{
    public int Id { get; set; }

    [Required, MaxLength(20)]
    public string Channel { get; set; } = "email";

    [Required, MaxLength(200)]
    public string To { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Subject { get; set; }

    [Required]
    public string Body { get; set; } = string.Empty;

    public bool IsHtml { get; set; }
    public bool IsSent { get; set; }
    public DateTime? SentAtUtc { get; set; }

    [MaxLength(500)]
    public string? Error { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(450)]
    public string? UserId { get; set; }
}

/// <summary>In-process / bus event for a delivered in-app notification.</summary>
public sealed record NotificationDeliveredEvent(
    int NotificationId,
    string UserId,
    NotificationKind Kind,
    string Title,
    string? Body,
    string? LinkUrl,
    DateTime CreatedAtUtc);
