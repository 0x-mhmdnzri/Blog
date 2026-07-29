using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace BlogApp.Models;

/// <summary>
/// Extended Identity user.
/// Roles: SuperAdmin, Author, Reader (bookmark + public account).
/// </summary>
public class ApplicationUser : IdentityUser
{
    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Bio { get; set; }

    public byte[]? ProfileImage { get; set; }

    [MaxLength(80)]
    public string? ProfileImageContentType { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Post> Posts { get; set; } = new List<Post>();
    public ICollection<PostBookmark> Bookmarks { get; set; } = new List<PostBookmark>();
}

public static class AppRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Author = "Author";
    /// <summary>Registered reader — can bookmark posts; no admin panel.</summary>
    public const string Reader = "Reader";
}

public static class AppClaims
{
    public const string CanModerateAllComments = "CanModerateAllComments";
    public const string CanManageAllPosts = "CanManageAllPosts";
    public const string CanViewAllAnalytics = "CanViewAllAnalytics";
}
