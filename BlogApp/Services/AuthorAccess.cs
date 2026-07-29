using System.Security.Claims;
using BlogApp.Models;

namespace BlogApp.Services;

/// <summary>
/// Helpers for RBAC ownership checks.
/// SuperAdmin (or claim holders) can see/manage everything;
/// Authors only their own posts and related comments.
/// </summary>
public static class AuthorAccess
{
    public static string? UserId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier);

    public static bool IsSuperAdmin(ClaimsPrincipal user) =>
        user.IsInRole(AppRoles.SuperAdmin);

    public static bool CanManageAllPosts(ClaimsPrincipal user) =>
        IsSuperAdmin(user) || user.HasClaim(AppClaims.CanManageAllPosts, "true");

    public static bool CanModerateAllComments(ClaimsPrincipal user) =>
        IsSuperAdmin(user) || user.HasClaim(AppClaims.CanModerateAllComments, "true");

    public static bool CanViewAllAnalytics(ClaimsPrincipal user) =>
        IsSuperAdmin(user) || user.HasClaim(AppClaims.CanViewAllAnalytics, "true");

    public static bool OwnsPost(ClaimsPrincipal user, Post post) =>
        CanManageAllPosts(user) || post.AuthorId == UserId(user);

    public static bool OwnsPost(ClaimsPrincipal user, string authorId) =>
        CanManageAllPosts(user) || authorId == UserId(user);
}
