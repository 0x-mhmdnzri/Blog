using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace AVICRM.Models;

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
        (CanViewAllAnalytics, "مشاهده همه آمار", "View all analytics", "📊"),
        (CanManageUsers, "مدیریت کاربران", "Manage users", "👥"),
        (CanManageRoles, "مدیریت نقش‌ها و مجوزها", "Manage roles & permissions", "🔐"),
        (CanManageSettings, "تنظیمات سایت", "Site settings", "⚙️"),
        (CanManageThemes, "مدیریت تم‌ها", "Manage themes", "🎨"),
        (CanManageMedia, "مدیریت رسانه", "Manage media", "🖼"),
        (CanManageTaxonomy, "طبقه‌بندی", "Taxonomy", "🏷"),
        (CanManageSeo, "ابزارهای سئو", "SEO tools", "🔍"),
        (CanManageNewsletter, "خبرنامه", "Newsletter", "✉"),
        (CanManageMonetization, "درآمدزایی", "Monetization", "💰"),
        (CanViewAudit, "گزارش ممیزی", "Audit log", "📋"),
        (CanManageApiKeys, "کلیدهای API", "API keys", "🔑"),
    };
}
