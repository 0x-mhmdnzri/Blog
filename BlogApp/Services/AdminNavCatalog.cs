using System.Security.Claims;
using BlogApp.Models;

namespace BlogApp.Services;

public sealed class AdminNavItem
{
    public required string Key { get; init; }
    public required string LabelKey { get; init; }
    public required string Controller { get; init; }
    public required string Action { get; init; }
    public required string Icon { get; init; }
    public string? GroupKey { get; init; }
    public bool SuperAdminOnly { get; init; }
    /// <summary>Visible to any authenticated staff (Author or SuperAdmin).</summary>
    public bool StaffOnly { get; init; }
    public bool DemoTag { get; init; }
    public bool SuperOnlyTag { get; init; }
    public bool ShowPendingBadge { get; init; }

    public bool Matches(string controller, string action)
    {
        if (!string.Equals(Controller, controller, StringComparison.OrdinalIgnoreCase))
            return false;

        return Key switch
        {
            "posts" => action is "Posts" or "Create" or "Edit",
            "settings" => string.Equals(Controller, "AdminSettings", StringComparison.OrdinalIgnoreCase)
                          && action is "Index",
            "flags" => string.Equals(Controller, "AdminSettings", StringComparison.OrdinalIgnoreCase)
                       && action is "FeatureFlags",
            "taxonomy" => string.Equals(controller, "Taxonomy", StringComparison.OrdinalIgnoreCase),
            "analytics" => string.Equals(controller, "AdminAnalytics", StringComparison.OrdinalIgnoreCase),
            "authorintel" => string.Equals(controller, "AdminAuthorIntel", StringComparison.OrdinalIgnoreCase),
            "profile" => string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase)
                         && action is "Profile",
            "authors" => string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase)
                         && action is "Authors" or "CreateAuthor",
            "notifications" => string.Equals(controller, "AdminNotifications", StringComparison.OrdinalIgnoreCase),
            "moderation" => string.Equals(controller, "AdminModeration", StringComparison.OrdinalIgnoreCase),
            "reports" => string.Equals(controller, "AdminReports", StringComparison.OrdinalIgnoreCase),
            "users" => string.Equals(controller, "AdminUsers", StringComparison.OrdinalIgnoreCase),
            "roles" => string.Equals(controller, "AdminRoles", StringComparison.OrdinalIgnoreCase),
            "audit" => string.Equals(controller, "AdminAudit", StringComparison.OrdinalIgnoreCase),
            "enterprise" => string.Equals(controller, "AdminEnterprise", StringComparison.OrdinalIgnoreCase),
            "backup" => string.Equals(controller, "AdminBackup", StringComparison.OrdinalIgnoreCase),
            "apikeys" => string.Equals(controller, "AdminApiKeys", StringComparison.OrdinalIgnoreCase),
            "myapikeys" => string.Equals(controller, "AccountApiKeys", StringComparison.OrdinalIgnoreCase),
            "accessibility" => string.Equals(controller, "AdminAccessibility", StringComparison.OrdinalIgnoreCase),
            "themes" => string.Equals(controller, "AdminThemes", StringComparison.OrdinalIgnoreCase),
            "dashboard" => string.Equals(controller, "Admin", StringComparison.OrdinalIgnoreCase)
                           && action is "Index",
            "seo" => string.Equals(controller, "Admin", StringComparison.OrdinalIgnoreCase)
                     && action.StartsWith("Seo", StringComparison.OrdinalIgnoreCase),
            "media" => string.Equals(controller, "Admin", StringComparison.OrdinalIgnoreCase)
                       && action.StartsWith("Media", StringComparison.OrdinalIgnoreCase),
            "monetization" => string.Equals(controller, "AdminMonetization", StringComparison.OrdinalIgnoreCase),
            "newsletter" => string.Equals(controller, "AdminNewsletter", StringComparison.OrdinalIgnoreCase),
            _ => string.Equals(Action, action, StringComparison.OrdinalIgnoreCase)
        };
    }
}

public static class AdminNavCatalog
{
    public static readonly AdminNavItem[] All =
    {
        new() { Key = "dashboard", GroupKey = "admin.group.general", LabelKey = "admin.nav.dashboard",
            Controller = "Admin", Action = "Index",
            Icon = "M3 13h8V3H3v10zm0 8h8v-6H3v6zm10 0h8V11h-8v10zm0-18v6h8V3h-8z" },

        new() { Key = "moderation", GroupKey = "admin.group.general", LabelKey = "admin.nav.moderation",
            Controller = "AdminModeration", Action = "Index", ShowPendingBadge = true,
            Icon = "M12 1L3 5v6c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V5l-9-4zm0 10.99h7c-.53 4.12-3.28 7.79-7 8.94V12H5V6.3l7-3.11v8.8z" },

        new() { Key = "posts", GroupKey = "admin.group.general", LabelKey = "admin.nav.posts",
            Controller = "Admin", Action = "Posts", ShowPendingBadge = true,
            Icon = "M14 2H6c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z" },

        new() { Key = "comments", GroupKey = "admin.group.general", LabelKey = "admin.nav.comments",
            Controller = "Admin", Action = "Comments", ShowPendingBadge = true,
            Icon = "M21 6h-2v9H6v2c0 .55.45 1 1 1h11l4 4V7c0-.55-.45-1-1-1zm-4 6V3c0-.55-.45-1-1-1H3c-.55 0-1 .45-1 1v14l4-4h10c.55 0 1-.45 1-1z" },

        new() { Key = "reports", GroupKey = "admin.group.general", LabelKey = "admin.nav.reports", ShowPendingBadge = true,
            Controller = "AdminReports", Action = "Index",
            Icon = "M15.73 3H8.27L3 8.27v7.46L8.27 21h7.46L21 15.73V8.27L15.73 3zM12 17.3c-.72 0-1.3-.58-1.3-1.3s.58-1.3 1.3-1.3 1.3.58 1.3 1.3-.58 1.3-1.3 1.3zm1-4.3h-2V7h2v6z" },

        new() { Key = "notifications", GroupKey = "admin.group.general", LabelKey = "nav.notifications", ShowPendingBadge = true,
            Controller = "AdminNotifications", Action = "Index",
            Icon = "M12 22c1.1 0 2-.9 2-2h-4c0 1.1.9 2 2 2zm6-6v-5c0-3.07-1.63-5.64-4.5-6.32V4c0-.83-.67-1.5-1.5-1.5s-1.5.67-1.5 1.5v.68C7.64 5.36 6 7.92 6 11v5l-2 2v1h16v-1l-2-2z" },

        new() { Key = "themes", GroupKey = "admin.group.content", LabelKey = "admin.nav.themes",
            Controller = "AdminThemes", Action = "Index", StaffOnly = true, ShowPendingBadge = false,
            Icon = "M12 3c-4.97 0-9 4.03-9 9s4.03 9 9 9c.83 0 1.5-.67 1.5-1.5 0-.39-.15-.74-.39-1.01-.23-.26-.38-.61-.38-.99 0-.83.67-1.5 1.5-1.5H16c2.76 0 5-2.24 5-5 0-4.42-4.03-8-9-8zm-5.5 9c-.83 0-1.5-.67-1.5-1.5S5.67 9 6.5 9 8 9.67 8 10.5 7.33 12 6.5 12zm3-4C8.67 8 8 7.33 8 6.5S8.67 5 9.5 5s1.5.67 1.5 1.5S10.33 8 9.5 8zm5 0c-.83 0-1.5-.67-1.5-1.5S13.67 5 14.5 5s1.5.67 1.5 1.5S15.33 8 14.5 8zm3 4c-.83 0-1.5-.67-1.5-1.5S16.67 9 17.5 9s1.5.67 1.5 1.5-.67 1.5-1.5 1.5z" },

        new() { Key = "media", GroupKey = "admin.group.content", LabelKey = "admin.nav.media",
            Controller = "Admin", Action = "Media",
            Icon = "M21 19V5c0-1.1-.9-2-2-2H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2zM8.5 13.5l2.5 3.01L14.5 12l4.5 6H5l3.5-4.5z" },

        new() { Key = "taxonomy", GroupKey = "admin.group.content", LabelKey = "admin.nav.taxonomy",
            Controller = "Taxonomy", Action = "Index",
            Icon = "M17.63 5.84C17.27 5.33 16.67 5 16 5L5 5.01C3.9 5.01 3 5.9 3 7v10c0 1.1.9 1.99 2 1.99L16 19c.67 0 1.27-.33 1.63-.84L22 12l-4.37-6.16z" },

        new() { Key = "analytics", GroupKey = "admin.group.growth", LabelKey = "admin.nav.analytics",
            Controller = "AdminAnalytics", Action = "Index",
            Icon = "M3.5 18.49l6-6.01 4 4L22 6.92l-1.41-1.41-7.09 7.97-4-4L2 16.99z" },

        new() { Key = "authorintel", GroupKey = "admin.group.growth", LabelKey = "admin.nav.author_intel",
            Controller = "AdminAuthorIntel", Action = "Index", SuperAdminOnly = true, SuperOnlyTag = true,
            Icon = "M16 11c1.66 0 2.99-1.34 2.99-3S17.66 5 16 5c-1.66 0-3 1.34-3 3s1.34 3 3 3zm-8 0c1.66 0 2.99-1.34 2.99-3S9.66 5 8 5C6.34 5 5 6.34 5 8s1.34 3 3 3zm0 2c-2.33 0-7 1.17-7 3.5V19h14v-2.5c0-2.33-4.67-3.5-7-3.5zm8 0c-.29 0-.62.02-.97.05 1.16.84 1.97 1.97 1.97 3.45V19h6v-2.5c0-2.33-4.67-3.5-7-3.5z" },

        new() { Key = "seo", GroupKey = "admin.group.growth", LabelKey = "admin.nav.seo",
            Controller = "Admin", Action = "SeoTools",
            Icon = "M15.5 14h-.79l-.28-.27A6.471 6.471 0 0 0 16 9.5 6.5 6.5 0 1 0 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z" },

        new() { Key = "monetization", GroupKey = "admin.group.growth", LabelKey = "admin.nav.monetization", ShowPendingBadge = true,
            Controller = "AdminMonetization", Action = "Index", SuperAdminOnly = true,
            Icon = "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1.41 16.09V20h-2.67v-1.93c-1.71-.36-3.16-1.46-3.27-3.4h1.96c.1 1.05.82 1.87 2.65 1.87 1.96 0 2.4-.98 2.4-1.59 0-.83-.44-1.61-2.67-2.14-2.48-.6-4.18-1.62-4.18-3.67 0-1.72 1.39-2.84 3.11-3.21V4h2.67v1.95c1.86.45 2.79 1.86 2.85 3.39H14.3c-.05-1.11-.64-1.87-2.22-1.87-1.5 0-2.4.68-2.4 1.64 0 .84.65 1.39 2.67 1.91s4.18 1.39 4.18 3.91c-.01 1.83-1.38 2.83-3.12 3.16z" },

        new() { Key = "newsletter", GroupKey = "admin.group.growth", LabelKey = "admin.nav.newsletter",
            Controller = "AdminNewsletter", Action = "Index",
            Icon = "M20 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 4l-8 5-8-5V6l8 5 8-5v2z" },

        new() { Key = "profile", GroupKey = "admin.group.account", LabelKey = "admin.nav.profile",
            Controller = "Account", Action = "Profile",
            Icon = "M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z" },

        new() { Key = "myapikeys", GroupKey = "admin.group.account", LabelKey = "admin.nav.my_apikeys",
            Controller = "AccountApiKeys", Action = "Index",
            Icon = "M12.65 10C11.83 7.67 9.61 6 7 6c-3.31 0-6 2.69-6 6s2.69 6 6 6c2.61 0 4.83-1.67 5.65-4H17v4h4v-4h2v-4H12.65zM7 14c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2z" },

        new() { Key = "users", GroupKey = "admin.group.account", LabelKey = "admin.nav.users",
            Controller = "AdminUsers", Action = "Index", SuperAdminOnly = true,
            Icon = "M16 11c1.66 0 2.99-1.34 2.99-3S17.66 5 16 5c-1.66 0-3 1.34-3 3s1.34 3 3 3zm-8 0c1.66 0 2.99-1.34 2.99-3S9.66 5 8 5C6.34 5 5 6.34 5 8s1.34 3 3 3zm0 2c-2.33 0-7 1.17-7 3.5V19h14v-2.5c0-2.33-4.67-3.5-7-3.5zm8 0c-.29 0-.62.02-.97.05 1.16.84 1.97 1.97 1.97 3.45V19h6v-2.5c0-2.33-4.67-3.5-7-3.5z" },

        new() { Key = "roles", GroupKey = "admin.group.account", LabelKey = "admin.nav.roles",
            Controller = "AdminRoles", Action = "Index", SuperAdminOnly = true,
            Icon = "M12 1L3 5v6c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V5l-9-4z" },

        new() { Key = "authors", GroupKey = "admin.group.account", LabelKey = "admin.nav.authors",
            Controller = "Account", Action = "Authors", SuperAdminOnly = true,
            Icon = "M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z" },

        new() { Key = "apikeys", GroupKey = "admin.group.system", LabelKey = "admin.nav.apikeys", ShowPendingBadge = true,
            Controller = "AdminApiKeys", Action = "Index", SuperAdminOnly = true,
            Icon = "M12.65 10C11.83 7.67 9.61 6 7 6c-3.31 0-6 2.69-6 6s2.69 6 6 6c2.61 0 4.83-1.67 5.65-4H17v4h4v-4h2v-4H12.65zM7 14c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2z" },

        new() { Key = "accessibility", GroupKey = "admin.group.system", LabelKey = "admin.nav.accessibility",
            Controller = "AdminAccessibility", Action = "Index",
            Icon = "M12 2c1.1 0 2 .9 2 2s-.9 2-2 2-2-.9-2-2 .9-2 2-2zm9 7h-6v13h-2v-6h-2v6H9V9H3V7h18v2z" },

        new() { Key = "settings", GroupKey = "admin.group.system", LabelKey = "admin.nav.settings",
            Controller = "AdminSettings", Action = "Index", SuperAdminOnly = true,
            Icon = "M19.14 12.94c.04-.31.06-.63.06-.94 0-.31-.02-.63-.06-.94l2.03-1.58c.18-.14.23-.41.12-.61l-1.92-3.32c-.12-.22-.37-.29-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54c-.04-.24-.24-.41-.48-.41h-3.84c.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L2.74 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.04.31-.06.63-.06.94s.02.63.06.94l-2.03 1.58c-.18.14-.23.41-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z" },

        new() { Key = "flags", GroupKey = "admin.group.system", LabelKey = "admin.nav.flags",
            Controller = "AdminSettings", Action = "FeatureFlags", SuperAdminOnly = true,
            Icon = "M14.4 6L14 4H5v17h2v-7h5.6l.4 2h7V6z" },

        new() { Key = "enterprise", GroupKey = "admin.group.system", LabelKey = "admin.nav.enterprise",
            Controller = "AdminEnterprise", Action = "Index", SuperAdminOnly = true,
            Icon = "M12 7V3H2v18h20V7H12zM6 19H4v-2h2v2zm0-4H4v-2h2v2zm0-4H4V9h2v2zm0-4H4V5h2v2zm4 12H8v-2h2v2zm0-4H8v-2h2v2zm0-4H8V9h2v2zm0-4H8V5h2v2zm10 12h-8v-2h2v-2h-2v-2h2v-2h-2V9h8v10zm-2-8h-2v2h2v-2zm0 4h-2v2h2v-2z" },

        new() { Key = "backup", GroupKey = "admin.group.system", LabelKey = "admin.nav.backup",
            Controller = "AdminBackup", Action = "Index", SuperAdminOnly = true,
            Icon = "M19.35 10.04A7.49 7.49 0 0 0 12 4C9.11 4 6.6 5.64 5.35 8.04A5.994 5.994 0 0 0 0 14c0 3.31 2.69 6 6 6h13c2.76 0 5-2.24 5-5 0-2.64-2.05-4.78-4.65-4.96zM14 13v4h-4v-4H7l5-5 5 5h-3z" },

        new() { Key = "audit", GroupKey = "admin.group.system", LabelKey = "admin.nav.audit",
            Controller = "AdminAudit", Action = "Index", SuperAdminOnly = true,
            Icon = "M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-5 14H7v-2h7v2zm3-4H7v-2h10v2zm0-4H7V7h10v2z" },
    };

    public static IEnumerable<AdminNavItem> ForUser(ClaimsPrincipal user)
    {
        var isSuper = AuthorAccess.IsSuperAdmin(user);
        var isStaff = isSuper || user.IsInRole(AppRoles.Author);
        var hasPageClaims = user.Claims.Any(c => c.Type == AppClaims.Page);

        foreach (var item in All)
        {
            if (isSuper)
            {
                yield return item;
                continue;
            }

            if (user.HasClaim(AppClaims.Page, item.Key))
            {
                yield return item;
                continue;
            }

            if (hasPageClaims)
            {
                if (item.Key is "profile" or "myapikeys" or "dashboard")
                    yield return item;
                continue;
            }

            if (item.SuperAdminOnly) continue;
            if (item.StaffOnly && !isStaff) continue;
            if (!isStaff && item.Key is not ("profile")) continue;
            yield return item;
        }
    }
}
