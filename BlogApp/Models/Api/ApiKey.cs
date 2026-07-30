using System.ComponentModel.DataAnnotations;

namespace BlogApp.Models;

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

    /// <summary>SHA-256 hex of full secret (never store plaintext).</summary>
    [Required, MaxLength(64)]
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>Comma-separated scopes: read,write,webhooks</summary>
    [MaxLength(200)]
    public string Scopes { get; set; } = "read";

    public bool IsActive { get; set; } = true;
    public bool IsBanned { get; set; }
    [MaxLength(500)]
    public string? BanReason { get; set; }
    public DateTime? BannedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }

    public long RequestCount { get; set; }
    public int AbuseStrikeCount { get; set; }
    public DateTime? LastAbuseAtUtc { get; set; }
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
