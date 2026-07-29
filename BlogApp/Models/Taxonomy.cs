using System.ComponentModel.DataAnnotations;

namespace BlogApp.Models;

public class Category
{
    public int Id { get; set; }

    [Required, MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(90)]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(400)]
    public string? Description { get; set; }

    public int? ParentId { get; set; }
    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = new List<Category>();

    public int DisplayOrder { get; set; }

    public ICollection<Post> Posts { get; set; } = new List<Post>();
}

public class Tag
{
    public int Id { get; set; }

    [Required, MaxLength(60)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(70)]
    public stringSlug { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Description { get; set; }

    public ICollection<PostTag> PostTags { get; set; } = new List<PostTag>();
}

public class PostTag
{
    public int PostId { get; set; }
    public Post Post { get; set; } = null!;

    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}

public class PostSeries
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(140)]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<SeriesPost> Posts { get; set; } = new List<SeriesPost>();
}

public class SeriesPost
{
    public int SeriesId { get; set; }
    public PostSeries Series { get; set; } = null!;

    public int PostId { get; set; }
    public Post Post { get; set; } = null!;

    public int SortOrder { get; set; }
}

public class TopicCollection
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(140)]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsPublished { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<TopicCollectionItem> Items { get; set; } = new List<TopicCollectionItem>();
}

public class TopicCollectionItem
{
    public int Id { get; set; }

    public int TopicCollectionId { get; set; }
    public TopicCollection TopicCollection { get; set; } = null!;

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public int? TagId { get; set; }
    public Tag? Tag { get; set; }

    public int SortOrder { get; set; }
}

public class Comment
{
    public int Id { get; set; }

    public int PostId { get; set; }
    public Post Post { get; set; } = null!;

    [Required, MaxLength(80)]
    public string AuthorName { get; set; } = string.Empty;

    [Required, MaxLength(2000)]
    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public CommentStatus Status { get; set; } = CommentStatus.Pending;

    /// <summary>Denormalized like count for “relevant” sort.</summary>
    public int LikeCount { get; set; }

    public ICollection<CommentLike> Likes { get; set; } = new List<CommentLike>();
}

public enum CommentStatus
{
    Pending,
    Approved,
    Rejected
}
