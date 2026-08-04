using System.ComponentModel.DataAnnotations;
using BlogApp.Models;

namespace BlogApp.Models.ViewModels;

public class SiteSettingsViewModel
{
    [Required, MaxLength(120)]
    public string SiteName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? SiteDescription { get; set; }

    [MaxLength(120)]
    public string? AuthorName { get; set; }

    [MaxLength(80)]
    public string? TwitterHandle { get; set; }

    [MaxLength(200)]
    public string? BaseUrl { get; set; }

    public bool MaintenanceMode { get; set; }

    [MaxLength(500)]
    public string? MaintenanceMessage { get; set; }

    public bool AnnouncementEnabled { get; set; }

    [MaxLength(500)]
    public string? AnnouncementText { get; set; }

    [MaxLength(20)]
    public string AnnouncementStyle { get; set; } = "info";

    // ── SMTP (DB-backed; SuperAdmin only) ──
    public bool SmtpEnabled { get; set; }

    [MaxLength(200)]
    public string? SmtpHost { get; set; }

    [Range(1, 65535)]
    public int SmtpPort { get; set; } = 587;

    public bool SmtpEnableSsl { get; set; } = true;

    [MaxLength(200)]
    public string? SmtpUserName { get; set; }

    /// <summary>Leave blank on save to keep the existing password.</summary>
    [MaxLength(200)]
    public string? SmtpPassword { get; set; }

    /// <summary>True when a password is already stored (UI hint; never exposes the secret).</summary>
    public bool SmtpPasswordIsSet { get; set; }

    [MaxLength(200), EmailAddress]
    public string? SmtpFromAddress { get; set; }

    [MaxLength(120)]
    public string? SmtpFromDisplayName { get; set; }
}

public class AdminUserListItem
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public bool IsLockedOut { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public int PostCount { get; set; }
    public bool EmailConfirmed { get; set; }
}

public class AuditLogItem
{
    public int Id { get; set; }
    public string? ActorUserName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class ModerationQueueViewModel
{
    public List<AdminCommentListItem> PendingComments { get; set; } = new();
    public List<ContentReport> OpenReports { get; set; } = new();
    public List<PendingPostReviewItem> PendingPosts { get; set; } = new();
}

public class PendingPostReviewItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string LanguageCode { get; set; } = "fa";
    public string AuthorName { get; set; } = "";
    public string AuthorId { get; set; } = "";
    public string? Summary { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
