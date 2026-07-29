using System.ComponentModel.DataAnnotations;

namespace BlogApp.Models;

public class Post
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(220)]
    public string
        Slug { get; set; } = string.Empty;

    [MaxLength(400)]
    public string? Summary { get; set; }

    public string ContentMarkdown { get; set; } = string.Empty;

    public int? CoverMediaAssetId { get; set; }
    public MediaAsset? CoverMediaAsset { get; set; }

    [Required]
    public string AuthorId { get; set; } = string.Empty;
    public ApplicationUser Author { get; set; } = null!;

    public bool IsPublished { get; set; }
    public DateTime? ScheduledPublishAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsSticky { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public int ReadingTimeMinutes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public int ViewCount { get; set; }

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public ICollection<PostTag> PostTags { get; set; } = new List<PostTag>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<MediaAsset> Media { get; set; } = new List<MediaAsset>();
    public ICollection<PostView> Views { get; set; } = new List<PostView>();
    public ICollection<PostRevision> Revisions { get; set; } = new List<PostRevision>();
    public ICollection<SeriesPost> SeriesMemberships { get; set; } = new List<SeriesPost>();
}

public class PostView
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public Post Post { get; set; } = null!;
    public DateTime ViewedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(64)]
    public string VisitorHash { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? SessionKey { get; set; }

    [MaxLength(16)]
    public string? DeviceType { get; set; }

    [MaxLength(40)]
    public string? Browser { get; set; }

    [MaxLength(40)]
    public string? Os { get; set; }

    [MaxLength(40)]
    public string? TrafficSource { get; set; }

    [MaxLength(200)]
    public string? ReferrerHost { get; set; }

    [MaxLength(8)]
    public string? CountryCode { get; set; }
}

public class PostRevision
{
    public int Id { get; set; }

    public int PostId { get; set; }
    public Post Post { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(400)]
    public string? Summary { get; set; }

    public string ContentMarkdown { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(200)]
    public string? Note { get; set; }

    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }
}
