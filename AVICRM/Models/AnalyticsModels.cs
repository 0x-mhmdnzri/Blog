using System.ComponentModel.DataAnnotations;

namespace AVICRM.Models;

/// <summary>Site-wide visit session (bounce = only 1 page view in session).</summary>
public class AnalyticsSession
{
    public int Id { get; set; }

    [Required, MaxLength(64)]
    public string SessionKey { get; set; } = string.Empty;

    [MaxLength(64)]
    public string VisitorHash { get; set; } = string.Empty;

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;
    public int PageViewCount { get; set; }

    [MaxLength(16)]
    public string? DeviceType { get; set; }

    [MaxLength(40)]
    public string? Browser { get; set; }

    [MaxLength(40)]
    public string? Os { get; set; }

    [MaxLength(8)]
    public string? CountryCode { get; set; }

    [MaxLength(40)]
    public string? TrafficSource { get; set; }

    [MaxLength(400)]
    public string? ReferrerHost { get; set; }
}

/// <summary>Search box queries (keyword analytics).</summary>
public class SearchQueryLog
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Query { get; set; } = string.Empty;

    public int ResultCount { get; set; }
    public DateTime SearchedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(64)]
    public string? VisitorHash { get; set; }
}

/// <summary>Reading duration beacon from the post page.</summary>
public class ReadingDurationLog
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public Post Post { get; set; } = null!;

    /// <summary>Seconds the tab was visible before unload / heartbeat.</summary>
    public int DurationSeconds { get; set; }

    public DateTime LoggedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(64)]
    public string? VisitorHash { get; set; }
}

/// <summary>Click heatmap sample (normalized 0–1000 for x/y on post body).</summary>
public class HeatmapClick
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public Post Post { get; set; } = null!;

    /// <summary>0–1000 relative X within content box.</summary>
    public int X { get; set; }
    /// <summary>0–1000 relative Y within content box.</summary>
    public int Y { get; set; }

    public DateTime ClickedAtUtc { get; set; } = DateTime.UtcNow;
}
