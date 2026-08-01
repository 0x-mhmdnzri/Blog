using System.ComponentModel.DataAnnotations;

namespace AVICRM.Models;

public class SubscriptionPlan
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>Price in minor units of Currency (e.g. tomans or cents).</summary>
    public decimal Price { get; set; }

    [Required, MaxLength(8)]
    public string Currency { get; set; } = "IRT";

    /// <summary>0 = lifetime, otherwise days of access.</summary>
    public int DurationDays { get; set; } = 30;

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<UserSubscription> Subscriptions { get; set; } = new List<UserSubscription>();
}

public enum SubscriptionStatus
{
    Pending = 0,
    Active = 1,
    Expired = 2,
    Cancelled = 3
}

/// <summary>Paid membership instance for a user under a plan.</summary>
public class UserSubscription
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public int PlanId { get; set; }
    public SubscriptionPlan Plan { get; set; } = null!;

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Pending;

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndsAtUtc { get; set; }

    [MaxLength(200)]
    public string? PaymentReference { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public enum DonationStatus
{
    Pending = 0,
    Confirmed = 1,
    Rejected = 2
}

public class Donation
{
    public int Id { get; set; }

    [MaxLength(450)]
    public string? UserId { get; set; }

    [MaxLength(120)]
    public string? DonorName { get; set; }

    [MaxLength(200)]
    public string? DonorEmail { get; set; }

    public decimal Amount { get; set; }

    [Required, MaxLength(8)]
    public string Currency { get; set; } = "IRT";

    [MaxLength(500)]
    public string? Message { get; set; }

    public bool IsAnonymous { get; set; }

    public DonationStatus Status { get; set; } = DonationStatus.Pending;

    [MaxLength(200)]
    public string? PaymentReference { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAtUtc { get; set; }
}

public enum AdPlacement
{
    Header = 0,
    Sidebar = 1,
    InArticle = 2,
    Footer = 3
}

public class Advertisement
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public AdPlacement Placement { get; set; } = AdPlacement.Sidebar;

    /// <summary>HTML snippet or plain image+link markup (sanitized on render for trusted admins).</summary>
    public string HtmlContent { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? TargetUrl { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public DateTime? StartsAtUtc { get; set; }
    public DateTime? EndsAtUtc { get; set; }

    public int ImpressionCount { get; set; }
    public int ClickCount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class AffiliateLink
{
    public int Id { get; set; }

    [Required, MaxLength(40)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string DestinationUrl { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Network { get; set; }

    public bool IsActive { get; set; } = true;
    public int ClickCount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastClickAtUtc { get; set; }
}
