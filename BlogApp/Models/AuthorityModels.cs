using System.ComponentModel.DataAnnotations;

namespace BlogApp.Models;

/// <summary>P4.1 — operational tracker for quality backlink outreach.</summary>
public class BacklinkLead
{
    public int Id { get; set; }

    [Required, MaxLength(300)]
    public string TargetSite { get; set; } = "";

    [MaxLength(500)]
    public string? TargetUrl { get; set; }

    [MaxLength(500)]
    public string? OurUrl { get; set; }

    [MaxLength(200)]
    public string? Contact { get; set; }

    /// <summary>prospect | contacted | negotiated | acquired | lost | rejected</summary>
    [Required, MaxLength(24)]
    public string Status { get; set; } = "prospect";

    [MaxLength(40)]
    public string? Source { get; set; }

    public int? DomainRating { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? AcquiredAtUtc { get; set; }
}

/// <summary>P4.2 — quarterly DA/DR snapshot — manual entry from Ahrefs/Moz/Majestic.</summary>
public class AuthoritySnapshot
{
    public int Id { get; set; }

    [Required, MaxLength(16)]
    public string Period { get; set; } = "";

    public DateTime MeasuredAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(40)]
    public string Provider { get; set; } = "Ahrefs";

    public int? DomainRating { get; set; }
    public int? DomainAuthority { get; set; }
    public int? TrustFlow { get; set; }
    public int? CitationFlow { get; set; }
    public int? ReferringDomains { get; set; }
    public int? OrganicKeywords { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
