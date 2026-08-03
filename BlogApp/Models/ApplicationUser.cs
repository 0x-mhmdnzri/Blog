using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace BlogApp.Models;

public enum AuthorApplicationStatus
{
    None = 0,
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

/// <summary>Optional public gender for author presentation (him / her / prefer not to say).</summary>
public enum UserGender
{
    Unspecified = 0,
    Male = 1,
    Female = 2,
    Other = 3
}

/// <summary>
/// Extended Identity user.
/// Base hierarchy: SuperAdmin → Author → Reader.
/// Extra access is granted via claims on roles or users.
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

    /// <summary>Public gender presentation (optional).</summary>
    public UserGender Gender { get; set; } = UserGender.Unspecified;

    /// <summary>Twitter / X handle or full URL (without leading @ preferred).</summary>
    [MaxLength(120)]
    public string? Twitter { get; set; }

    [MaxLength(200)]
    public string? LinkedIn { get; set; }

    /// <summary>Telegram username or t.me link.</summary>
    [MaxLength(120)]
    public string? Telegram { get; set; }

    [MaxLength(40)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Website { get; set; }

    [MaxLength(120)]
    public string? GitHub { get; set; }

    [MaxLength(120)]
    public string? Instagram { get; set; }

    /// <summary>Public "become an author" application pipeline.</summary>
    public AuthorApplicationStatus AuthorApplicationStatus { get; set; } = AuthorApplicationStatus.None;

    [MaxLength(500)]
    public string? AuthorApplicationMessage { get; set; }

    public DateTime? AuthorAppliedAtUtc { get; set; }

    [MaxLength(500)]
    public string? AuthorReviewNote { get; set; }

    public ICollection<Post> Posts { get; set; } = new List<Post>();
    public ICollection<PostBookmark> Bookmarks { get; set; } = new List<PostBookmark>();
}

public static class AppRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Author = "Author";
    /// <summary>Registered reader — can bookmark posts; no admin panel by default.</summary>
    public const string Reader = "Reader";

    public static readonly string[] BuiltIn = { SuperAdmin, Author, Reader };

    public static bool IsBuiltIn(string role) =>
        BuiltIn.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
}

public static class AppClaims
{
    /// <summary>Claim type for admin page access. Value = AdminNavItem.Key</summary>
    public const string Page = "perm.page";

    public const string CanModerateAllComments = "CanModerateAllComments";
    public const string CanManageAllPosts = "CanManageAllPosts";
    public const string CanViewAllAnalytics = "CanViewAllAnalytics";
    public const string CanManageUsers = "CanManageUsers";
    public const string CanManageRoles = "CanManageRoles";
    public const string CanManageSettings = "CanManageSettings";
    public const string CanManageThemes = "CanManageThemes";
    public const string CanManageMedia = "CanManageMedia";
    public const string CanManageTaxonomy = "CanManageTaxonomy";
    public const string CanManageSeo = "CanManageSeo";
    public const string CanManageNewsletter = "CanManageNewsletter";
    public const string CanManageMonetization = "CanManageMonetization";
    public const string CanViewAudit = "CanViewAudit";
    public const string CanManageApiKeys = "CanManageApiKeys";

    public static readonly (string Type, string LabelFa, string LabelEn, string Icon)[] Capabilities =
    {
        (CanManageAllPosts, "مدیریت همه نوشته‌ها", "Manage all posts", "📝"),
        (CanModerateAllComments, "مدیریت همه دیدگاه‌ها", "Moderate all comments", "💬"),
        (CanViewAllAnalytics, "مشاهده تحلیل‌ها", "View all analytics", "📊"),
        (CanManageUsers, "مدیریت کاربران", "Manage users", "👥"),
        (CanManageRoles, "مدیریت نقش‌ها", "Manage roles", "🔑"),
        (CanManageSettings, "تنظیمات سایت", "Site settings", "⚙️"),
        (CanManageThemes, "تم‌ها", "Themes", "🎨"),
        (CanManageMedia, "رسانه‌ها", "Media library", "🖼️"),
        (CanManageTaxonomy, "دسته‌ها و برچسب‌ها", "Taxonomy", "🏷️"),
        (CanManageSeo, "ابزارهای سئو", "SEO tools", "🔍"),
        (CanManageNewsletter, "خبرنامه", "Newsletter", "✉"),
        (CanManageMonetization, "درآمدزایی", "Monetization", "💰"),
        (CanViewAudit, "گزارش ممیزی", "Audit log", "📋"),
        (CanManageApiKeys, "کلیدهای API", "API keys", "🔑"),
    };
}
