using System.ComponentModel.DataAnnotations;

namespace BlogApp.Models;

/// <summary>Key/value site configuration (settings, maintenance, announcement).</summary>
public class SiteSetting
{
    [Key, MaxLength(80)]
    public string Key { get; set; } = string.Empty;

    public string? Value { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Runtime feature toggles (no redeploy).</summary>
public class FeatureFlag
{
    [Key, MaxLength(80)]
    public string Key { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(400)]
    public string? Description { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Immutable admin/staff action log for audit dashboard.</summary>
public class AuditLog
{
    public int Id { get; set; }

    [MaxLength(450)]
    public string? ActorUserId { get; set; }

    [MaxLength(100)]
    public string? ActorUserName { get; set; }

    [Required, MaxLength(80)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? EntityType { get; set; }

    [MaxLength(80)]
    public string? EntityId { get; set; }

    [MaxLength(1000)]
    public string? Details { get; set; }

    [MaxLength(64)]
    public string? IpAddress { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public enum ContentReportTarget
{
    Post = 0,
    Comment = 1
}

public enum ContentReportStatus
{
    Open = 0,
    Resolved = 1,
    Dismissed = 2
}

/// <summary>User-submitted report against a post or comment.</summary>
public class ContentReport
{
    public int Id { get; set; }

    public ContentReportTarget TargetType { get; set; }
    public int TargetId { get; set; }

    [MaxLength(220)]
    public string? TargetTitle { get; set; }

    [Required, MaxLength(80)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Details { get; set; }

    [MaxLength(450)]
    public string? ReporterUserId { get; set; }

    [MaxLength(100)]
    public string? ReporterName { get; set; }

    public ContentReportStatus Status { get; set; } = ContentReportStatus.Open;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAtUtc { get; set; }

    [MaxLength(450)]
    public string? ResolvedByUserId { get; set; }
}

/// <summary>Well-known setting keys.</summary>
public static class SiteSettingKeys
{
    public const string SiteName = "SiteName";
    public const string SiteDescription = "SiteDescription";
    public const string AuthorName = "AuthorName";
    public const string TwitterHandle = "TwitterHandle";
    public const string BaseUrl = "BaseUrl";
    public const string MaintenanceMode = "MaintenanceMode";
    public const string MaintenanceMessage = "MaintenanceMessage";
    public const string AnnouncementEnabled = "AnnouncementEnabled";
    public const string AnnouncementText = "AnnouncementText";
    public const string AnnouncementStyle = "AnnouncementStyle"; // info | warn | success
    /// <summary>Bumped when announcement content changes so dismissed users see the new banner.</summary>
    public const string AnnouncementVersion = "AnnouncementVersion";
}

/// <summary>Default feature flag keys seeded on bootstrap.</summary>
public static class FeatureFlagKeys
{
    public const string Comments = "CommentsEnabled";
    public const string Registration = "RegistrationEnabled";
    public const string Bookmarks = "BookmarksEnabled";
    public const string Search = "SearchEnabled";
    public const string AiAssist = "AiAssistEnabled";
    public const string PublicReports = "PublicReportsEnabled";
}
