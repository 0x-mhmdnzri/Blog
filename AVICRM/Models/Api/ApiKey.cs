using System.ComponentModel.DataAnnotations;

namespace AVICRM.Models;

public enum ApiKeyApprovalStatus : byte
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

public class ApiKey
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    /// <summary>User-visible label (like GitHub PAT name).</summary>
    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Public prefix shown in UI, e.g. blog_ab12…</summary>
    [Required, MaxLength(16)]
    public string KeyPrefix { get; set; } = string.Empty;

    /// <summary>SHA-256 hex of full secret (used for auth).</summary>
    [Required, MaxLength(64)]
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>
    /// Data-Protection ciphertext of the full token so the owner can copy later.
    /// Null for legacy keys created before this field existed.
    /// </summary>
    [MaxLength(2000)]
    public string? EncryptedToken { get; set; }

    /// <summary>Comma-separated scopes: read,write,webhooks</summary>
    [MaxLength(200)]
    public string Scopes { get; set; } = "read";

    public bool IsActive { get; set; } = true;
    public bool IsBanned { get; set; }
    [MaxLength(500)]
    public string? BanReason { get; set; }
    public DateTime? BannedAtUtc { get; set; }

    /// <summary>SuperAdmin must approve before the key can authenticate API calls.</summary>
    public ApiKeyApprovalStatus ApprovalStatus { get; set; } = ApiKeyApprovalStatus.Pending;

    public DateTime? ApprovedAtUtc { get; set; }

    [MaxLength(450)]
    public string? ApprovedByUserId { get; set; }

    [MaxLength(500)]
    public string? RejectionReason { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }

    public long RequestCount { get; set; }
    public int AbuseStrikeCount { get; set; }
    public DateTime? LastAbuseAtUtc { get; set; }

    /// <summary>Usable for API auth only when active, not banned, approved, and not expired.</summary>
    public bool IsUsable =>
        IsActive
        && !IsBanned
        && ApprovalStatus == ApiKeyApprovalStatus.Approved
        && (ExpiresAtUtc is null || ExpiresAtUtc > DateTime.UtcNow);

    public bool CanRevealToken => !string.IsNullOrEmpty(EncryptedToken);
}

public class WebhookSubscription
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    public int? ApiKeyId { get; set; }
    public ApiKey? ApiKey { get; set; }

    [Required, MaxLength(500)]
    public string TargetUrl { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Secret { get; set; } = string.Empty;

    /// <summary>Comma events: post.published,comment.created</summary>
    [MaxLength(300)]
    public string Events { get; set; } = "post.published";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastDeliveryAtUtc { get; set; }
    public int FailureCount { get; set; }
}

public static class ApiScopes
{
    public const string Read = "read";
    public const string Write = "write";
    public const string Webhooks = "webhooks";

    public static bool Has(string scopes, string scope) =>
        scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(s => string.Equals(s, scope, StringComparison.OrdinalIgnoreCase)
                      || string.Equals(s, "*", StringComparison.Ordinal));
}
