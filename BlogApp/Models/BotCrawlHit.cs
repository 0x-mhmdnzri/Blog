using System.ComponentModel.DataAnnotations;

namespace BlogApp.Models;

/// <summary>
/// One crawler request (search or AI). Ground truth for crawl-budget analysis.
/// Written async via channel so request path stays cheap.
/// </summary>
public class BotCrawlHit
{
    public long Id { get; set; }

    public DateTime HitAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Normalized family: googlebot, bingbot, gptbot, claudebot, …</summary>
    [Required, MaxLength(40)]
    public string BotFamily { get; set; } = "other";

    /// <summary>search | ai | archive | other</summary>
    [Required, MaxLength(16)]
    public string BotKind { get; set; } = "other";

    [MaxLength(300)]
    public string? UserAgent { get; set; }

    [Required, MaxLength(16)]
    public string Method { get; set; } = "GET";

    [Required, MaxLength(500)]
    public string Path { get; set; } = "/";

    [MaxLength(300)]
    public string? Query { get; set; }

    public int StatusCode { get; set; }

    /// <summary>Server-side elapsed milliseconds for this request.</summary>
    public int ElapsedMs { get; set; }

    [MaxLength(64)]
    public string? IpHash { get; set; }
}
