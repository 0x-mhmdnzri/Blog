using System.ComponentModel.DataAnnotations;

namespace AVICRM.Models;

/// <summary>
/// Persistent HTTP redirect (301 permanent / 302 temporary) used when a post slug
/// changes or when an admin adds a manual redirect. Matched by exact FromPath
/// (leading slash, no host) before MVC routing.
/// </summary>
public class RedirectRule
{
    public int Id { get; set; }

    /// <summary>Source path, e.g. "/post/old-slug". Always stored with leading slash, no query string.</summary>
    [Required, MaxLength(500)]
    public string FromPath { get; set; } = string.Empty;

    /// <summary>Destination: absolute URL or site-relative path, e.g. "/post/new-slug" or "https://...".</summary>
    [Required, MaxLength(1000)]
    public string ToUrl { get; set; } = string.Empty;

    /// <summary>301 = permanent (default), 302 = temporary.</summary>
    public int StatusCode { get; set; } = 301;

    public bool IsActive { get; set; } = true;

    [MaxLength(300)]
    public string? Notes { get; set; }

    public int HitCount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastHitAtUtc { get; set; }
}
