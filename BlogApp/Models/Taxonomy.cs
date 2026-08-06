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
    public string Slug { get; set; } = string.Empty;

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

/// <summary>Finder-style folder for organizing posts (independent of category/tag).</summary>
public class PostFolder
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Description { get; set; }

    /// <summary>Optional tint: blue, yellow, red, green, purple, gray, orange (macOS-like).</summary>
    [MaxLength(20)]
    public string Color { get; set; } = "blue";

    public int? ParentId { get; set; }
    public PostFolder? Parent { get; set; }
    public ICollection<PostFolder> Children { get; set; } = new List<PostFolder>();

    /// <summary>Owner author; SuperAdmin sees all.</summary>
    [Required, MaxLength(450)]
    public string OwnerUserId { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<PostFolderItem> Items { get; set; } = new List<PostFolderItem>();
}

public class PostFolderItem
{
    public int FolderId { get; set; }
    public PostFolder Folder { get; set; } = null!;

    public int PostId { get; set; }
    public Post Post { get; set; } = null!;

    public int SortOrder { get; set; }
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
}
