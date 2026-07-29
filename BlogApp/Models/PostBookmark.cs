using System.ComponentModel.DataAnnotations;

namespace BlogApp.Models;

/// <summary>Saved post for a signed-in reader (or author/admin).</summary>
public class PostBookmark
{
    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public int PostId { get; set; }
    public Post Post { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
