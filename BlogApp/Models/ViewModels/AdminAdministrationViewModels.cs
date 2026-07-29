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
}
