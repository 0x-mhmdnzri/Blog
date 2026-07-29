using System.ComponentModel.DataAnnotations;

namespace BlogApp.Models;

public enum NotificationKind
{
    System = 0,
    NewComment = 1,
    CommentApproved = 2,
    NewFollower = 3,
    WeeklyDigest = 4,
    AdCampaign = 5
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

    /// <summary>Optional phone for SMS ads — user supplies; never required.</summary>
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

/// <summary>Queued outbound message (email/SMS) — process with your own worker or hosted service.</summary>
public class OutboundMessage
{
    public int Id { get; set; }

    [Required, MaxLength(20)]
    public string Channel { get; set; } = "email"; // email | sms | push

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
