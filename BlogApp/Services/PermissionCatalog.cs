using BlogApp.Models;
using System.Security.Claims;

namespace BlogApp.Services;

/// <summary>
/// Server-side hierarchical permission tree built from AdminNavCatalog + capability claims.
/// Used by the Roles &amp; Permissions admin UI and by nav filtering.
/// </summary>
public static class PermissionCatalog
{
    public sealed class PageNode
    {
        public required string Key { get; init; }
        public required string GroupKey { get; init; }
        public required string LabelKey { get; init; }
        public required string Icon { get; init; }
        public required string Controller { get; init; }
        public required string Action { get; init; }
        public bool SuperAdminOnly { get; init; }
        public bool StaffOnly { get; init; }
    }

    public sealed class GroupNode
    {
        public required string GroupKey { get; init; }
        public required IReadOnlyList<PageNode> Pages { get; init; }
    }

    public sealed class CapabilityNode
    {
        public required string ClaimType { get; init; }
        public required string LabelFa { get; init; }
        public required string LabelEn { get; init; }
        public required string Icon { get; init; }
    }

    public static IReadOnlyList<GroupNode> GetPageTree()
    {
        return AdminNavCatalog.All
            .GroupBy(i => i.GroupKey ?? "admin.group.general")
            .Select(g => new GroupNode
            {
                GroupKey = g.Key,
                Pages = g.Select(i => new PageNode
                {
                    Key = i.Key,
                    GroupKey = i.GroupKey ?? "admin.group.general",
                    LabelKey = i.LabelKey,
                    Icon = i.Icon,
                    Controller = i.Controller,
                    Action = i.Action,
                    SuperAdminOnly = i.SuperAdminOnly,
                    StaffOnly = i.StaffOnly
                }).ToList()
            })
            .ToList();
    }

    public static IReadOnlyList<CapabilityNode> GetCapabilities() =>
        AppClaims.Capabilities.Select(c => new CapabilityNode
        {
            ClaimType = c.Type,
            LabelFa = c.LabelFa,
            LabelEn = c.LabelEn,
            Icon = c.Icon
        }).ToList();

    public static bool UserHasPage(ClaimsPrincipal user, string pageKey)
    {
        if (AuthorAccess.IsSuperAdmin(user)) return true;
        return user.HasClaim(AppClaims.Page, pageKey);
    }

    public static bool UserHasCapability(ClaimsPrincipal user, string claimType)
    {
        if (AuthorAccess.IsSuperAdmin(user)) return true;
        return user.HasClaim(claimType, "true");
    }
}
