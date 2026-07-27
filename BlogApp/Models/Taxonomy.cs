using System.ComponentModel.DataAnnotations;

namespace BlogApp.Models;

public class Category
{
    public int Id { get; set; }

    [Required, MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(90)]
    public string Slug { get; set; } = string.Empty;

    public ICollection<Post> Posts { get; set; } = new List<Post>();
}

public class Tag
{
    public int Id { get; set; }

    [Required, MaxLength(60)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(70)]
    public string Slug { get; set; } = string.Empty;

    public ICollection<PostTag> PostTags { get; set; } = new List<PostTag>();
}

/// <summary>Many-to-many join between Post and Tag.</summary>
public class PostTag
{
    public int PostId { get; set; }
    public Post Post { get; set; } = null!;

    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;
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

    public bool IsApproved { get; set; }
}
