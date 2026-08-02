using System.Security.Claims;
using AVICRM.Models;

namespace AVICRM.Services;

public sealed class AdminNavItem
{
    public required string Key { get; init; }
    public required string LabelKey { get; init; }
    public required string Controller { get; init; }
    public required string Action { get; init; }
    public required string Icon { get; init; }
    public string? GroupKey { get; init; }
    public bool SuperAdminOnly { get; init; }
    public bool StaffOnly { get; init; }
    public bool DemoTag { get; init; }
    public bool SuperOnlyTag { get; init; }
    public bool ShowPendingBadge { get; init; }
    public AdminNavItem[]? Children { get; init; }
    public bool IsSection => Children is { Length: > 0 };

    public bool Matches(string controller, string action)
    {
        if (IsSection)
            return Children!.Any(c => c.Matches(controller, action));

        if (!string.Equals(Controller, controller, StringComparison.OrdinalIgnoreCase))
            return false;

        return Key switch
        {
            "dashboard" => action is "Index",
            "settings" => string.Equals(Controller, "AdminSettings", StringComparison.OrdinalIgnoreCase) && action is "Index",
            "flags" => string.Equals(Controller, "AdminSettings", StringComparison.OrdinalIgnoreCase) && action is "FeatureFlags",
            "analytics" => string.Equals(controller, "AdminAnalytics", StringComparison.OrdinalIgnoreCase),
            "profile" => string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase) && action is "Profile",
            "notifications" => string.Equals(controller, "AdminNotifications", StringComparison.OrdinalIgnoreCase),
            "users" => string.Equals(controller, "AdminUsers", StringComparison.OrdinalIgnoreCase),
            "roles" => string.Equals(controller, "AdminRoles", StringComparison.OrdinalIgnoreCase),
            "audit" => string.Equals(controller, "AdminAudit", StringComparison.OrdinalIgnoreCase),
            "enterprise" => string.Equals(controller, "AdminEnterprise", StringComparison.OrdinalIgnoreCase),
            "backup" => string.Equals(controller, "AdminBackup", StringComparison.OrdinalIgnoreCase),
            "apikeys" => string.Equals(controller, "AdminApiKeys", StringComparison.OrdinalIgnoreCase),
            "jobs" => string.Equals(controller, "AdminBackgroundJobs", StringComparison.OrdinalIgnoreCase),
            "a11y" => string.Equals(controller, "AdminAccessibility", StringComparison.OrdinalIgnoreCase),
            "search" => string.Equals(controller, "AdminSearch", StringComparison.OrdinalIgnoreCase),
            "newsletter" => string.Equals(controller, "AdminNewsletter", StringComparison.OrdinalIgnoreCase),
            "myapikeys" => string.Equals(controller, "AccountApiKeys", StringComparison.OrdinalIgnoreCase),
            _ => string.Equals(Action, action, StringComparison.OrdinalIgnoreCase)
        };
    }
}

/// <summary>AVICRM admin sidebar — hierarchical menu aligned with FEATURES.md.</summary>
public static class AdminNavCatalog
{
    const string IDash = "M3 13h8V3H3v10zm0 8h8v-6H3v6zm10 0h8V11h-8v10zm0-18v6h8V3h-8z";
    const string IPeople = "M16 11c1.66 0 2.99-1.34 2.99-3S17.66 5 16 5c-1.66 0-3 1.34-3 3s1.34 3 3 3zm-8 0c1.66 0 2.99-1.34 2.99-3S9.66 5 8 5C6.34 5 5 6.34 5 8s1.34 3 3 3zm0 2c-2.33 0-7 1.17-7 3.5V19h14v-2.5c0-2.33-4.67-3.5-7-3.5zm8 0c-.29 0-.62.02-.97.05 1.16.84 1.97 1.97 1.97 3.45V19h6v-2.5c0-2.33-4.67-3.5-7-3.5z";
    const string IBiz = "M12 7V3H2v18h20V7H12zM6 19H4v-2h2v2zm0-4H4v-2h2v2zm0-4H4V9h2v2zm0-4H4V5h2v2zm4 12H8v-2h2v2zm0-4H8v-2h2v2zm0-4H8V9h2v2zm0-4H8V5h2v2zm10 12h-8v-2h2v-2h-2v-2h2v-2h-2V9h8v10zm-2-8h-2v2h2v-2zm0 4h-2v2h2v-2z";
    const string ILead = "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z";
    const string IOpp = "M3.5 18.49l6-6.01 4 4L22 6.92l-1.41-1.41-7.09 7.97-4-4L2 16.99z";
    const string ITask = "M19 3h-4.18C14.4 1.84 13.3 1 12 1c-1.3 0-2.4.84-2.82 2H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-7 0c.55 0 1 .45 1 1s-.45 1-1 1-1-.45-1-1 .45-1 1-1zm-2 14l-4-4 1.41-1.41L10 14.17l6.59-6.59L18 9l-8 8z";
    const string IMail = "M20 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 4l-8 5-8-5V6l8 5 8-5v2z";
    const string IAuto = "M19.43 12.98c.04-.32.07-.64.07-.98s-.03-.66-.07-.98l2.11-1.65c.19-.15.24-.42.12-.64l-2-3.46c-.12-.22-.39-.3-.61-.22l-2.49 1c-.52-.4-1.08-.73-1.69-.98l-.38-2.65C14.46 2.18 14.25 2 14 2h-4c-.25 0-.46.18-.49.42l-.38 2.65c-.61.25-1.17.59-1.69.98l-2.49-1c-.23-.09-.49 0-.61.22l-2 3.46c-.13.22-.07.49.12.64l2.11 1.65c-.04.32-.07.65-.07.98s.03.66.07.98l-2.11 1.65c-.19.15-.24.42-.12.64l2 3.46c.12.22.39.3.61.22l2.49-1c.52.4 1.08.73 1.69.98l.38 2.65c.03.24.24.42.49.42h4c.25 0 .46-.18.49-.42l.38-2.65c.61-.25 1.17-.59 1.69-.98l2.49 1c.23.09.49 0 .61-.22l2-3.46c.12-.22.07-.49-.12-.64l-2.11-1.65zM12 15.5c-1.93 0-3.5-1.57-3.5-3.5s1.57-3.5 3.5-3.5 3.5 1.57 3.5 3.5-1.57 3.5-3.5 3.5z";
    const string IChart = "M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zM9 17H7v-7h2v7zm4 0h-2V7h2v10zm4 0h-2v-4h2v4z";
    const string ICase = "M20 6h-4V4c0-1.11-.89-2-2-2h-4c-1.11 0-2 .89-2 2v2H4c-1.11 0-1.99.89-1.99 2L2 19c0 1.11.89 2 2 2h16c1.11 0 2-.89 2-2V8c0-1.11-.89-2-2-2zm-6 0h-4V4h4v2z";
    const string ICamp = "M18 2H6c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zM6 4h5v8l-2.5-1.5L6 12V4z";
    const string IPlug = "M17 16l-4-4V8.82C14.16 8.4 15 7.3 15 6c0-1.66-1.34-3-3-3S9 4.34 9 6c0 1.3.84 2.4 2 2.82V12l-4 4H3v5h5v-3.05l4-4.2 4 4.2V21h5v-5h-4z";
    const string IShield = "M12 1L3 5v6c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V5l-9-4z";
    const string IKey = "M12.65 10C11.83 7.67 9.61 6 7 6c-3.31 0-6 2.69-6 6s2.69 6 6 6c2.61 0 4.83-1.67 5.65-4H17v4h4v-4h2v-4H12.65zM7 14c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2z";
    const string ISet = "M19.14 12.94c.04-.31.06-.63.06-.94 0-.31-.02-.63-.06-.94l2.03-1.58a.49.49 0 0 0 .12-.61l-1.92-3.32a.49.49 0 0 0-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54a.484.484 0 0 0-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L2.74 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.04.31-.06.63-.06.94s.02.63.06.94l-2.03 1.58a.49.49 0 0 0-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6A3.6 3.6 0 1 1 12 8.4a3.6 3.6 0 0 1 0 7.2z";
    const string IBackup = "M19.35 10.04C18.67 6.59 15.64 4 12 4 9.11 4 6.6 5.64 5.35 8.04 2.34 8.36 0 10.91 0 14c0 3.31 2.69 6 6 6h13c2.76 0 5-2.24 5-5 0-2.64-2.05-4.78-4.65-4.96zM14 13v4h-4v-4H7l5-5 5 5h-3z";
    const string IBell = "M12 22c1.1 0 2-.9 2-2h-4c0 1.1.89 2 2 2zm6-6v-5c0-3.07-1.64-5.64-4.5-6.32V4c0-.83-.67-1.5-1.5-1.5s-1.5.67-1.5 1.5v.68C7.63 5.36 6 7.92 6 11v5l-2 2v1h16v-1l-2-2z";
    const string ISearch = "M15.5 14h-.79l-.28-.27A6.471 6.471 0 0 0 16 9.5 6.5 6.5 0 1 0 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z";
    const string IUser = "M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z";
    const string IJob = "M20 6h-4V4c0-1.11-.89-2-2-2h-4c-1.11 0-2 .89-2 2v2H4c-1.11 0-1.99.89-1.99 2L2 19c0 1.11.89 2 2 2h16c1.11 0 2-.89 2-2V8c0-1.11-.89-2-2-2zm-6 0h-4V4h4v2z";
    const string IA11y = "M12 2c1.1 0 2 .9 2 2s-.9 2-2 2-2-.9-2-2 .9-2 2-2zm9 7h-6v13h-2v-6h-2v6H9V9H3V7h18v2z";
    const string IAi = "M21 11.01L3 11v2h18zM3 16h12v2H3zM21 6H3v2.01L21 8z";

    static AdminNavItem Leaf(string key, string labelKey, string controller, string action, string icon,
        bool super = false, bool staff = false, bool demo = false, bool superTag = false, bool badge = false)
        => new()
        {
            Key = key, LabelKey = labelKey, Controller = controller, Action = action, Icon = icon,
            SuperAdminOnly = super, StaffOnly = staff, DemoTag = demo, SuperOnlyTag = superTag, ShowPendingBadge = badge
        };

    static AdminNavItem Section(string key, string labelKey, string icon, params AdminNavItem[] children)
        => new() { Key = key, LabelKey = labelKey, Controller = "", Action = "", Icon = icon, Children = children };

    public static readonly AdminNavItem[] All =
    {
        Leaf("dashboard", "crm.nav.dashboard", "Admin", "Index", IDash),

        Section("crm.core", "crm.nav.group.core", IPeople,
            Leaf("crm.contacts", "crm.nav.contacts", "Admin", "ComingSoon", IPeople, demo: true),
            Leaf("crm.accounts", "crm.nav.accounts", "Admin", "ComingSoon", IBiz, demo: true),
            Leaf("crm.leads", "crm.nav.leads", "Admin", "ComingSoon", ILead, demo: true),
            Leaf("crm.opportunities", "crm.nav.opportunities", "Admin", "ComingSoon", IOpp, demo: true),
            Leaf("crm.activities", "crm.nav.activities", "Admin", "ComingSoon", ITask, demo: true),
            Leaf("crm.import", "crm.nav.import", "Admin", "ComingSoon", ISearch, demo: true)),

        Section("crm.sales", "crm.nav.group.sales", IOpp,
            Leaf("crm.pipeline", "crm.nav.pipeline", "Admin", "ComingSoon", IOpp, demo: true),
            Leaf("crm.forecast", "crm.nav.forecast", "Admin", "ComingSoon", IChart, demo: true),
            Leaf("crm.quotes", "crm.nav.quotes", "Admin", "ComingSoon", ICamp, demo: true),
            Leaf("crm.products", "crm.nav.products", "Admin", "ComingSoon", IBiz, demo: true)),

        Section("crm.comm", "crm.nav.group.comm", IMail,
            Leaf("crm.email", "crm.nav.email", "Admin", "ComingSoon", IMail, demo: true),
            Leaf("crm.templates", "crm.nav.templates", "Admin", "ComingSoon", ICamp, demo: true),
            Leaf("crm.tasks", "crm.nav.tasks", "Admin", "ComingSoon", ITask, demo: true),
            Leaf("crm.calendar", "crm.nav.calendar", "Admin", "ComingSoon", ITask, demo: true),
            Leaf("crm.sequences", "crm.nav.sequences", "Admin", "ComingSoon", IMail, demo: true)),

        Section("crm.service", "crm.nav.group.service", ICase,
            Leaf("crm.cases", "crm.nav.cases", "Admin", "ComingSoon", ICase, demo: true),
            Leaf("crm.kb", "crm.nav.kb", "Admin", "ComingSoon", ISearch, demo: true),
            Leaf("crm.portal", "crm.nav.portal", "Admin", "ComingSoon", IPeople, demo: true),
            Leaf("crm.sla", "crm.nav.sla", "Admin", "ComingSoon", ITask, demo: true)),

        Section("crm.marketing", "crm.nav.group.marketing", ICamp,
            Leaf("newsletter", "crm.nav.campaigns", "AdminNewsletter", "Index", ICamp, staff: true),
            Leaf("crm.lists", "crm.nav.lists", "Admin", "ComingSoon", IPeople, demo: true),
            Leaf("crm.consent", "crm.nav.consent", "Admin", "ComingSoon", IShield, demo: true),
            Leaf("crm.attribution", "crm.nav.attribution", "Admin", "ComingSoon", IChart, demo: true)),

        Section("crm.automation", "crm.nav.group.automation", IAuto,
            Leaf("crm.workflows", "crm.nav.workflows", "Admin", "ComingSoon", IAuto, demo: true),
            Leaf("crm.assignment", "crm.nav.assignment", "Admin", "ComingSoon", IUser, demo: true),
            Leaf("crm.validation", "crm.nav.validation", "Admin", "ComingSoon", ITask, demo: true),
            Leaf("crm.customfields", "crm.nav.customfields", "Admin", "ComingSoon", ILead, demo: true)),

        Section("crm.analytics", "crm.nav.group.analytics", IChart,
            Leaf("analytics", "crm.nav.analytics", "AdminAnalytics", "Index", IChart, staff: true),
            Leaf("crm.reports", "crm.nav.reports", "AdminReports", "Index", IChart, staff: true),
            Leaf("search", "crm.nav.search", "AdminSearch", "Index", ISearch, staff: true),
            Leaf("crm.dataquality", "crm.nav.dataquality", "Admin", "ComingSoon", ISearch, demo: true)),

        Section("crm.integrations", "crm.nav.group.integrations", IPlug,
            Leaf("apikeys", "crm.nav.apikeys", "AdminApiKeys", "Index", IKey, super: true, superTag: true),
            Leaf("myapikeys", "crm.nav.myapikeys", "AccountApiKeys", "Index", IKey),
            Leaf("crm.webhooks", "crm.nav.webhooks", "Admin", "ComingSoon", IPlug, demo: true),
            Leaf("crm.connectors", "crm.nav.connectors", "Admin", "ComingSoon", IPlug, demo: true),
            Leaf("crm.sso", "crm.nav.sso", "Admin", "ComingSoon", IShield, demo: true)),

        Section("crm.security", "crm.nav.group.security", IShield,
            Leaf("users", "crm.nav.users", "AdminUsers", "Index", IPeople, super: true, superTag: true),
            Leaf("roles", "crm.nav.roles", "AdminRoles", "Index", IShield, super: true, superTag: true),
            Leaf("audit", "crm.nav.audit", "AdminAudit", "Index", IChart, super: true, superTag: true),
            Leaf("enterprise", "crm.nav.enterprise", "AdminEnterprise", "Index", IBiz, super: true, superTag: true)),

        Section("crm.platform", "crm.nav.group.platform", ISet,
            Leaf("settings", "crm.nav.settings", "AdminSettings", "Index", ISet, super: true, superTag: true),
            Leaf("flags", "crm.nav.flags", "AdminSettings", "FeatureFlags", IAuto, super: true, superTag: true),
            Leaf("backup", "crm.nav.backup", "AdminBackup", "Index", IBackup, super: true, superTag: true),
            Leaf("jobs", "crm.nav.jobs", "AdminBackgroundJobs", "Index", IJob, super: true, superTag: true),
            Leaf("notifications", "crm.nav.notifications", "AdminNotifications", "Index", IBell, staff: true),
            Leaf("a11y", "crm.nav.a11y", "AdminAccessibility", "Index", IA11y, staff: true),
            Leaf("crm.i18n", "crm.nav.i18n", "Admin", "ComingSoon", ISearch, demo: true),
            Leaf("crm.ai", "crm.nav.ai", "Admin", "ComingSoon", IAi, demo: true)),

        Section("crm.aihub", "crm.nav.group.ai", IAi,
            Leaf("crm.ai.scoring", "crm.nav.ai_scoring", "Admin", "ComingSoon", IChart, demo: true),
            Leaf("crm.ai.forecast", "crm.nav.ai_forecast", "Admin", "ComingSoon", IChart, demo: true),
            Leaf("crm.ai.nba", "crm.nav.ai_nba", "Admin", "ComingSoon", IAi, demo: true),
            Leaf("crm.ai.notes", "crm.nav.ai_notes", "Admin", "ComingSoon", IMail, demo: true),
            Leaf("crm.ai.churn", "crm.nav.ai_churn", "Admin", "ComingSoon", IOpp, demo: true)),

        Leaf("profile", "crm.nav.profile", "Account", "Profile", IUser),
    };

    public static IEnumerable<AdminNavItem> ForUser(ClaimsPrincipal user)
    {
        var isSuper = user.IsInRole(AppRoles.SuperAdmin);
        var isAuthor = user.IsInRole(AppRoles.Author);
        var isStaff = isSuper || isAuthor;
        var hasPageClaims = user.Claims.Any(c => c.Type == AppClaims.Page);

        foreach (var item in All)
        {
            if (item.IsSection)
            {
                var kids = item.Children!.Where(Visible).ToArray();
                if (kids.Length == 0) continue;
                yield return new AdminNavItem
                {
                    Key = item.Key,
                    LabelKey = item.LabelKey,
                    Controller = item.Controller,
                    Action = item.Action,
                    Icon = item.Icon,
                    Children = kids
                };
                continue;
            }

            if (Visible(item))
                yield return item;
        }

        bool Visible(AdminNavItem item)
        {
            if (isSuper) return true;
            if (item.SuperAdminOnly) return false;
            if (user.HasClaim(AppClaims.Page, item.Key)) return true;
            if (hasPageClaims)
                return item.Key is "profile" or "myapikeys" or "dashboard";
            if (item.StaffOnly && !isStaff) return false;
            if (!isStaff && item.Key is not "profile") return false;
            return true;
        }
    }
}
