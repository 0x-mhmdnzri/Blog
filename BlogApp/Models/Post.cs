using System.ComponentModel.DataAnnotations;

namespace BlogApp.Models;

public class Post
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>URL-friendly identifier, e.g. "building-a-dark-pro-blog". Unique.</summary>
    [Required, MaxLength(220)]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(400)]
    public string? Summary { get; set; }

    /// <summary>
    /// The raw README-style Markdown the author writes. Stored in full (TEXT column),
    /// re-rendered to HTML on read via MarkdownService.
    /// </summary>
    public string ContentMarkdown { get; set; } = string.Empty;

    /// <summary>Optional cover image, stored as a MediaAsset.</summary>
    public int? CoverMediaAssetId { get; set; }
    public MediaAsset? CoverMediaAsset { get; set; }

    /// <summary>Owning author (Identity user). Null only for legacy rows before multi-author.</summary>
    [Required]
    public string AuthorId { get; set; } = string.Empty;
    public ApplicationUser Author { get; set; } = null!;

    public bool IsPublished { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public int ViewCount { get; set; }

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public ICollection<PostTag> PostTags { get; set; } = new List<PostTag>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();

    /// <summary>Every image/video/file referenced from ContentMarkdown.</summary>
    public ICollection<MediaAsset> Media { get; set; } = new List<MediaAsset>();

    /// <summary>Per-visit log backing analytics time-series charts.</summary>
    public ICollection<PostView> Views { get; set; } = new List<PostView>();
}

/// <summary>One row per de-duplicated page view.</summary>
public class PostView
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public Post Post { get; set; } = null!;
    public DateTime ViewedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(64)]
    public string VisitorHash { get; set; } = string.Empty;
}
