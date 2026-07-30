using System.Security.Claims;

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
            "profile" => string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase)
                         && action is "Profile",
            "authors" => string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase)
                         && action is "Authors" or "CreateAuthor",
            "notifications" => string.Equals(controller, "AdminNotifications", StringComparison.OrdinalIgnoreCase),
            "moderation" => string.Equals(controller, "AdminModeration", StringComparison.OrdinalIgnoreCase),
            "reports" => string.Equals(controller, "AdminReports", StringComparison.OrdinalIgnoreCase),
            "users" => string.Equals(controller, "AdminUsers", StringComparison.OrdinalIgnoreCase),
            "audit" => string.Equals(controller, "AdminAudit", StringComparison.OrdinalIgnoreCase),
            "dashboard" => string.Equals(controller, "Admin", StringComparison.OrdinalIgnoreCase)
                           && action is "Index",
            "seo" => string.Equals(controller, "Admin", StringComparison.OrdinalIgnoreCase)
                     && action is "SeoTools" or "SeoSaveMeta" or "SeoAddRedirect" or "SeoToggleRedirect"
                         or "SeoDeleteRedirect" or "SeoScanBrokenLinks",
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
            Controller = "AdminModeration", Action = "Index",
            Icon = "M12 1L3 5v6c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V5l-9-4zm0 10.99h7c-.53 4.12-3.28 7.79-7 8.94V12H5V6.3l7-3.11v8.8z" },

        new() { Key = "posts", GroupKey = "admin.group.general", LabelKey = "admin.nav.posts",
            Controller = "Admin", Action = "Posts",
            Icon = "M14 2H6c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z" },

        new() { Key = "comments", GroupKey = "admin.group.general", LabelKey = "admin.nav.comments",
            Controller = "Admin", Action = "Comments", ShowPendingBadge = true,
            Icon = "M21.99 4c0-1.1-.89-2-1.99-2H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h14l4 4-.01-18zM18 14H6v-2h12v2zm0-3H6V9h12v2zm0-3H6V6h12v2z" },

        new() { Key = "reports", GroupKey = "admin.group.general", LabelKey = "admin.nav.reports",
            Controller = "AdminReports", Action = "Index",
            Icon = "M15.73 3H8.27L3 8.27v7.46L8.27 21h7.46L21 15.73V8.27L15.73 3zM12 17.3c-.72 0-1.3-.58-1.3-1.3s.58-1.3 1.3-1.3 1.3.58 1.3 1.3-.58 1.3-1.3 1.3zm1-4.3h-2V7h2v6z" },

        new() { Key = "notifications", GroupKey = "admin.group.general", LabelKey = "nav.notifications",
            Controller = "AdminNotifications", Action = "Index",
            Icon = "M12 22c1.1 0 2-.9 2-2h-4c0 1.1.9 2 2 2zm6-6v-5c0-3.07-1.63-5.64-4.5-6.32V4c0-.83-.67-1.5-1.5-1.5s-1.5.67-1.5 1.5v.68C7.64 5.36 6 7.92 6 11v5l-2 2v1h16v-1l-2-2zm-2 1H8v-6c0-2.48 1.51-4.5 4-4.5s4 2.02 4 4.5v6z" },

        new() { Key = "media", GroupKey = "admin.group.content", LabelKey = "admin.nav.media",
            Controller = "Admin", Action = "Media", DemoTag = true,
            Icon = "M21 19V5c0-1.1-.9-2-2-2H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2zM8.5 13.5l2.5 3.01L14.5 12l4.5 6H5l3.5-4.5z" },

        new() { Key = "taxonomy", GroupKey = "admin.group.content", LabelKey = "admin.nav.taxonomy",
            Controller = "Taxonomy", Action = "Categories",
            Icon = "M17.63 5.84C17.27 5.33 16.67 5 16 5L5 5.01C3.9 5.01 3 5.9 3 7v10c0 1.1.9 1.99 2 1.99L16 19c.67 0 1.27-.33 1.63-.84L22 12l-4.37-6.16z" },

        new() { Key = "analytics", GroupKey = "admin.group.growth", LabelKey = "admin.nav.analytics",
            Controller = "AdminAnalytics", Action = "Index",
            Icon = "M3.5 18.49l6-6.01 4 4L22 6.92l-1.41-1.41-7.09 7.97-4-4L2 16.99z" },

        new() { Key = "seo", GroupKey = "admin.group.growth", LabelKey = "admin.nav.seo",
            Controller = "Admin", Action = "SeoTools",
            Icon = "M15.5 14h-.79l-.28-.27C15.41 12.59 16 11.11 16 9.5 16 5.91 13.09 3 9.5 3S3 5.91 3 9.5 5.91 16 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z" },

        new() { Key = "newsletter", GroupKey = "admin.group.growth", LabelKey = "admin.nav.newsletter",
            Controller = "Admin", Action = "Newsletter", DemoTag = true,
            Icon = "M20 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 4l-8 5-8-5V6l8 5 8-5v2z" },

        new() { Key = "profile", GroupKey = "admin.group.account", LabelKey = "admin.nav.profile",
            Controller = "Account", Action = "Profile",
            Icon = "M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z" },

        new() { Key = "users", GroupKey = "admin.group.account", LabelKey = "admin.nav.users",
            Controller = "AdminUsers", Action = "Index", SuperAdminOnly = true,
            Icon = "M16 11c1.66 0 2.99-1.34 2.99-3S17.66 5 16 5c-1.66 0-3 1.34-3 3s1.34 3 3 3zm-8 0c1.66 0 2.99-1.34 2.99-3S9.66 5 8 5C6.34 5 5 6.34 5 8s1.34 3 3 3zm0 2c-2.33 0-7 1.17-7 3.5V19h14v-2.5c0-2.33-4.67-3.5-7-3.5zm8 0c-.29 0-.62.02-.97.05 1.16.84 1.97 1.97 1.97 3.45V19h6v-2.5c0-2.33-4.67-3.5-7-3.5z" },

        new() { Key = "authors", GroupKey = "admin.group.account", LabelKey = "admin.nav.authors",
            Controller = "Account", Action = "Authors", SuperAdminOnly = true,
            Icon = "M12 3L1 9l4 2.18v6L12 21l7-3.82v-6l2-1.09V17h2V9L12 3zm6.82 6L12 12.72 5.18 9 12 5.28 18.82 9zM17 15.99l-5 2.73-5-2.73v-3.72L12 15l5-2.73v3.72z" },

        new() { Key = "settings", GroupKey = "admin.group.system", LabelKey = "admin.nav.settings",
            Controller = "AdminSettings", Action = "Index", SuperAdminOnly = true,
            Icon = "M19.14 12.94c.04-.31.06-.63.06-.94 0-.31-.02-.63-.06-.94l2.03-1.58c.18-.14.23-.41.12-.61l-1.92-3.32c-.12-.22-.37-.29-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54c-.04-.24-.24-.41-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L2.74 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.04.31-.06.63-.06.94s.02.63.06.94l-2.03 1.58c-.18.14-.23.41-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z" },

        new() { Key = "flags", GroupKey = "admin.group.system", LabelKey = "admin.nav.flags",
            Controller = "AdminSettings", Action = "FeatureFlags", SuperAdminOnly = true,
            Icon = "M14.4 6L14 4H5v17h2v-7h5.6l.4 2h7V6z" },

        new() { Key = "audit", GroupKey = "admin.group.system", LabelKey = "admin.nav.audit",
            Controller = "AdminAudit", Action = "Index", SuperAdminOnly = true,
            Icon = "M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-5 14H7v-2h7v2zm3-4H7v-2h10v2zm0-4H7V7h10v2z" },

        new() { Key = "settings_stub", GroupKey = "admin.group.system", LabelKey = "admin.nav.settings",
            Controller = "Admin", Action = "Settings", SuperOnlyTag = true,
            Icon = "M19.14 12.94c.04-.31.06-.63.06-.94 0-.31-.02-.63-.06-.94l2.03-1.58c.18-.14.23-.41.12-.61l-1.92-3.32c-.12-.22-.37-.29-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54c-.04-.24-.24-.41-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L2.74 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.04.31-.06.63-.06.94s.02.63.06.94l-2.03 1.58c-.18.14-.23.41-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z" },
    };

    public static IEnumerable<AdminNavItem> ForUser(ClaimsPrincipal user)
    {
        var isSuper = AuthorAccess.IsSuperAdmin(user);
        foreach (var item in All)
        {
            if (item.Key == "settings_stub")
            {
                if (!isSuper) yield return item;
                continue;
            }
            if (item.SuperAdminOnly && !isSuper) continue;
            yield return item;
        }
    }
}
