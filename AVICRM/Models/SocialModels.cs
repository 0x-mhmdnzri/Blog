using System.ComponentModel.DataAnnotations;

namespace AVICRM.Models;

public enum ReactionKind
{
    Like = 0,
    Love = 1,
    Clap = 2,
    Insight = 3,
    Funny = 4
}

public enum ActivityKind
{
    PostLiked = 0,
    PostReaction = 1,
    AuthorFollowed = 2,
    CategoryFollowed = 3,
    PostBookmarked = 4,
    CommentPosted = 5,
    Mention = 6,
    PostPublished = 7
}

/// <summary>Simple post like (binary heart).</summary>
public class PostLike
{
    public int PostId { get; set; }
    public Post Post { get; set; } = null!;

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Emoji-style reaction on a post (one kind per user per post).</summary>
public class PostReaction
{
    public int PostId { get; set; }
    public Post Post { get; set; } = null!;

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public ReactionKind Kind { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Reader follows a category for the activity feed.</summary>
public class CategoryFollow
{
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Append-only activity stream for social feed.</summary>
public class UserActivity
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string ActorUserId { get; set; } = string.Empty;
    public ApplicationUser Actor { get; set; } = null!;

    public ActivityKind Kind { get; set; }

    public int? PostId { get; set; }
    public int? CategoryId { get; set; }

    [MaxLength(450)]
    public string? TargetUserId { get; set; }

    [MaxLength(200)]
    public string? Title { get; set; }

    [MaxLength(400)]
    public string? LinkUrl { get; set; }

    [MaxLength(40)]
    public string? Meta { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>@username mention inside a comment (or post body later).</summary>
public class UserMention
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string MentionedUserId { get; set; } = string.Empty;

    [Required, MaxLength(450)]
    public string ActorUserId { get; set; } = string.Empty;

    public int? CommentId { get; set; }
    public int? PostId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
