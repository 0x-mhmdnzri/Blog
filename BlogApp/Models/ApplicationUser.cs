using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace BlogApp.Models;

/// <summary>
/// Extended Identity user for multi-author blog.
/// Roles: SuperAdmin (sees everything), Author (owns own posts/comments).
/// Claims can further refine permissions (e.g. CanModerateComments).
/// </summary>
public class ApplicationUser : IdentityUser
{
    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Bio { get; set; }

    /// <summary>Profile image stored as bytes in the DB (same pattern as MediaAsset).</summary>
    public byte[]? ProfileImage { get; set; }

    [MaxLength(80)]
    public string? ProfileImageContentType { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Post> Posts { get; set; } = new List<Post>();
}

public static class AppRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Author = "Author";
}

public static class AppClaims
{
    public const string CanModerateAllComments = "CanModerateAllComments";
    public const string CanManageAllPosts = "CanManageAllPosts";
    public const string CanViewAllAnalytics = "CanViewAllAnalytics";
}
