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
    /// The raw README-style Markdown the author writes: headings, paragraphs, fenced code blocks
    /// with language tags, images (![]()), video embeds via the custom {{video:mediaId}} token,
    /// tables, block quotes, task lists, etc. This is the single source of truth — stored in full,
    /// with no length limit (TEXT column), and re-rendered to HTML on read via MarkdownService.
    /// </summary>
    public string ContentMarkdown { get; set; } = string.Empty;

    /// <summary>Optional cover image, stored as a MediaAsset.</summary>
    public int? CoverMediaAssetId { get; set; }
    public MediaAsset? CoverMediaAsset { get; set; }

    public bool IsPublished { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public int ViewCount { get; set; }

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public ICollection<PostTag> PostTags { get; set; } = new List<PostTag>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();

    /// <summary>Every image/video/file referenced from ContentMarkdown, so nothing orphans in the DB.</summary>
    public ICollection<MediaAsset> Media { get; set; } = new List<MediaAsset>();

    /// <summary>Per-visit log (real reader traffic only) backing the analytics dashboard's
    /// time-series charts. ViewCount above stays a fast all-time counter; this is what
    /// makes "views over time" and "top posts this week" possible.</summary>
    public ICollection<PostView> Views { get; set; } = new List<PostView>();
}

/// <summary>One row per de-duplicated page view. No raw IP/User-Agent is stored — only a
/// one-way hash (see VisitorIdentity.ComputeHash), just enough to tell "the same visitor
/// reloading" apart from "a new visitor" for a short window.</summary>
public class PostView
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public Post Post { get; set; } = null!;
    public DateTime ViewedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(64)]
    public string VisitorHash { get; set; } = string.Empty;
}
