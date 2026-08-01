using System.ComponentModel.DataAnnotations;

namespace AVICRM.Models;

public enum NewsletterSubscriberStatus
{
    Pending = 0,
    Confirmed = 1,
    Unsubscribed = 2,
    Bounced = 3
}

public enum NewsletterCampaignStatus
{
    Draft = 0,
    Scheduled = 1,
    Sending = 2,
    Sent = 3,
    Cancelled = 4
}

/// <summary>Email list subscriber (guest or linked user) with double opt-in.</summary>
public class NewsletterSubscriber
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? Name { get; set; }

    [MaxLength(450)]
    public string? UserId { get; set; }

    public NewsletterSubscriberStatus Status { get; set; } = NewsletterSubscriberStatus.Pending;

    /// <summary>ISO language preference (fa/en/ar).</summary>
    [MaxLength(8)]
    public string LanguageCode { get; set; } = AppCultures.Default;

    /// <summary>Comma-separated segment tags e.g. readers,devs</summary>
    [MaxLength(400)]
    public string? SegmentTags { get; set; }

    [Required, MaxLength(64)]
    public string ConfirmToken { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string UnsubscribeToken { get; set; } = string.Empty;

    public DateTime SubscribedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAtUtc { get; set; }
    public DateTime? UnsubscribedAtUtc { get; set; }

    [MaxLength(64)]
    public string? Source { get; set; }
}

/// <summary>Named audience segment (filter expression stored as tags / status / language).</summary>
public class NewsletterSegment
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(400)]
    public string? Description { get; set; }

    /// <summary>If set, only this language.</summary>
    [MaxLength(8)]
    public string? LanguageCode { get; set; }

    /// <summary>Required tag that subscriber.SegmentTags must contain (optional).</summary>
    [MaxLength(80)]
    public string? RequiredTag { get; set; }

    /// <summary>Only Confirmed by default when true.</summary>
    public bool ConfirmedOnly { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Email campaign / scheduled newsletter blast.</summary>
public class NewsletterCampaign
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Subject { get; set; } = string.Empty;

    /// <summary>HTML body.</summary>
    public string BodyHtml { get; set; } = string.Empty;

    /// <summary>Optional plain-text fallback.</summary>
    public string? BodyText { get; set; }

    public int? SegmentId { get; set; }
    public NewsletterSegment? Segment { get; set; }

    /// <summary>If no segment: send to all confirmed (when empty filter).</summary>
    [MaxLength(8)]
    public string? LanguageFilter { get; set; }

    [MaxLength(80)]
    public string? TagFilter { get; set; }

    public NewsletterCampaignStatus Status { get; set; } = NewsletterCampaignStatus.Draft;

    public DateTime? ScheduledAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }

    public int RecipientCount { get; set; }
    public int SentCount { get; set; }
    public int FailCount { get; set; }

    [Required, MaxLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
