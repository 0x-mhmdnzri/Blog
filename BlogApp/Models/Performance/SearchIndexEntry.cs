using System.ComponentModel.DataAnnotations;

namespace BlogApp.Models;

/// <summary>Denormalized full-text search row (FTS-friendly columns for SQLite LIKE / future FTS5).</summary>
public class SearchIndexEntry
{
    public int Id { get; set; }

    public int PostId { get; set; }
    public Post? Post { get; set; }

    [Required, MaxLength(8)]
    public string LanguageCode { get; set; } = "fa";

    [Required, MaxLength(260)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(260)]
    public string Slug { get; set; } = string.Empty;

    public string? Summary { get; set; }

    /// <summary>Flattened body text for search (no markdown).</summary>
    public string? BodyText { get; set; }

    public string? TagsCsv { get; set; }
    public string? CategoryName { get; set; }

    public bool IsPublished { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public DateTime IndexedAtUtc { get; set; } = DateTime.UtcNow;
}
