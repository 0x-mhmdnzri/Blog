using System.ComponentModel.DataAnnotations;

namespace BlogApp.Models;

/// <summary>One like per user per comment. Guests cannot like.</summary>
public class CommentLike
{
    public int CommentId { get; set; }
    public Comment Comment { get; set; } = null!;

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
