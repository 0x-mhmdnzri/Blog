using System.ComponentModel.DataAnnotations;

namespace BlogApp.Models;

public enum CommentStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Spam = 3
}

/// <summary>
/// Threaded post comment. Guests may post (rate-limited); registered users get edit window + likes.
/// </summary>
public class Comment
{
    public int Id { get; set; }

    public int PostId { get; set; }
    public Post Post { get; set; } = null!;

    /// <summary>Reply parent (null = top-level).</summary>
    public int? ParentId { get; set; }
    public Comment? Parent { get; set; }
    public ICollection<Comment> Replies { get; set; } = new List<Comment>();

    /// <summary>Registered author when authenticated; null for pure guests.</summary>
    [MaxLength(450)]
    public string? UserId { get; set; }

    [Required, MaxLength(80)]
    public string AuthorName { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? AuthorEmail { get; set; }

    public bool IsGuest { get; set; }

    [Required]
    public string Body { get; set; } = string.Empty;

    public CommentStatus Status { get; set; } = CommentStatus.Pending;

    public int LikeCount { get; set; }

    public bool IsPinned { get; set; }
    public DateTime? PinnedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EditedAtUtc { get; set; }
    public int EditCount { get; set; }

    /// <summary>0–100 heuristic score; ≥ threshold → Spam status.</summary>
    public int SpamScore { get; set; }

    [MaxLength(500)]
    public string? SpamReasons { get; set; }

    /// <summary>SHA-256 truncated hash of client IP for abuse tracking (not reversible PII store).</summary>
    [MaxLength(64)]
    public string? IpHash { get; set; }

    public ICollection<CommentLike> Likes { get; set; } = new List<CommentLike>();
}
