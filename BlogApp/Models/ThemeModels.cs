using System.ComponentModel.DataAnnotations;

namespace BlogApp.Models;

public enum ThemeApprovalStatus
{
    Draft = 0,
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

/// <summary>User-created color theme. Only Approved themes can be activated site-wide.</summary>
public class CustomTheme
{
    public int Id { get; set; }

    [Required, MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(240)]
    public string? Description { get; set; }

    [MaxLength(450)]
    public string? OwnerUserId { get; set; }

    public ApplicationUser? Owner { get; set; }

    public ThemeApprovalStatus Status { get; set; } = ThemeApprovalStatus.Draft;

    [MaxLength(400)]
    public string? RejectionReason { get; set; }

    /// <summary>System preset (Dark Pro / Light) — always approved, not deletable by users.</summary>
    public bool IsSystem { get; set; }

    /// <summary>Currently active site theme (at most one).</summary>
    public bool IsActive { get; set; }

    // Core tokens (hex #RRGGBB)
    [Required, MaxLength(9)] public string Bg { get; set; } = "#0b0e14";
    [Required, MaxLength(9)] public string Surface { get; set; } = "#12161f";
    [Required, MaxLength(9)] public string Surface2 { get; set; } = "#171c27";
    [Required, MaxLength(9)] public string Border { get; set; } = "#232838";
    [Required, MaxLength(9)] public string Text { get; set; } = "#e6e9f0";
    [Required, MaxLength(9)] public string TextMuted { get; set; } = "#8b93a7";
    [Required, MaxLength(9)] public string Accent { get; set; } = "#e3b341";
    [Required, MaxLength(9)] public string Danger { get; set; } = "#e5637a";
    [Required, MaxLength(9)] public string Success { get; set; } = "#9ecb8c";

    /// <summary>dark | light — drives data-theme / data-bs-theme.</summary>
    [MaxLength(10)]
    public string Mode { get; set; } = "dark";

    public double ContrastTextOnBg { get; set; }
    public double ContrastMutedOnBg { get; set; }
    public double ContrastAccentOnBg { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAtUtc { get; set; }

    [MaxLength(450)]
    public string? ReviewedByUserId { get; set; }
}

public static class SiteSettingKeysThemes
{
    public const string ActiveThemeId = "ActiveThemeId";
}
