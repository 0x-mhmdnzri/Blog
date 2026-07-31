using System.ComponentModel.DataAnnotations;

namespace BlogApp.Models;

/// <summary>Unified admin search index (posts, comments, users, media, themes, pages).</summary>
public class AdminSearchDocument
{
    public long Id { get; set; }

    /// <summary>post | comment | user | media | theme | page | taxonomy</summary>
    [Required, MaxLength(32)]
    public string EntityType { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string EntityKey { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Subtitle { get; set; }

    public string? BodyText { get; set; }

    [MaxLength(400)]
    public string? Url { get; set; }

    [MaxLength(64)]
    public string? Icon { get; set; }

    [MaxLength(32)]
    public string? Status { get; set; }

    [MaxLength(8)]
    public string? LanguageCode { get; set; }

    public string? FacetsJson { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime IndexedAtUtc { get; set; } = DateTime.UtcNow;
    public int Boost { get; set; }
}
