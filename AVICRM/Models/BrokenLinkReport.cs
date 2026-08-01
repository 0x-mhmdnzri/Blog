using System.ComponentModel.DataAnnotations;

namespace AVICRM.Models;

/// <summary>
/// One row per broken internal link found during a scan of published post Markdown.
/// Cleared and re-written on each full scan.
/// </summary>
public class BrokenLinkReport
{
    public int Id { get; set; }

    public int PostId { get; set; }
    public Post Post { get; set; } = null!;

    [Required, MaxLength(200)]
    public string PostTitle { get; set; } = string.Empty;

    [Required, MaxLength(220)]
    public string PostSlug { get; set; } = string.Empty;

    /// <summary>The raw href found in Markdown (may be relative or absolute to this site).</summary>
    [Required, MaxLength(1000)]
    public string Url { get; set; } = string.Empty;

    /// <summary>Normalized path used for matching, e.g. "/post/missing-slug".</summary>
    [MaxLength(500)]
    public string? NormalizedPath { get; set; }

    public DateTime DetectedAtUtc { get; set; } = DateTime.UtcNow;
}
