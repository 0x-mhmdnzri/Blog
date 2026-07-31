using System.Security.Claims;
using BlogApp.Models;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

/// <summary>
/// Roles &amp; permissions matrix — SuperAdmin only.
/// Create roles, assign page + capability claims, attach roles to users.
/// </summary>
[Authorize(Roles = AppRoles.SuperAdmin)]
public class AdminRolesController : Controller
{
    private readonly RoleManager<IdentityRole> _roles;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IAuditService _audit;

    public AdminRolesController(
        RoleManager<IdentityRole> roles,
        UserManager<ApplicationUser> users,
        IAuditService audit)
    {
        _roles = roles;
        _users = users;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "نقش‌ها و مجوزها";
        var list = await _roles.Roles.OrderBy(r => r.Name).ToListAsync();
        var rows = new List<RoleListItem>();
        foreach (var r in list)
        {
            var claims = await _roles.GetClaimsAsync(r);
            var userCount = await _users.GetUsersInRoleAsync(r.Name!);
            rows.Add(new RoleListItem
            {
                Id = r.Id,
                Name = r.Name!,
                IsBuiltIn = AppRoles.IsBuiltIn(r.Name!),
                PageClaimCount = claims.Count(c => c.Type == AppClaims.Page),
                CapabilityCount = claims.Count(c => c.Type != AppClaims.Page && c.Value == "true"),
                UserCount = userCount.Count
            });
        }
        return View(rows);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name)
    {
        name = (name ?? "").Trim();
        if (name.Length is < 2 or > 64)
        {
            TempData["Error"] = "نام نقش باید بین ۲ تا ۶۴ کاراکتر باشد.";
            return RedirectToAction(nameof(Index));
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[A-Za-z][A-Za-z0-9_\-]{1,63}$"))
        {
            TempData["Error"] = "نام نقش فقط حروف لاتین، عدد، _ و - (شروع با حرف).";
            return RedirectToAction(nameof(Index));
        }

        if (await _roles.RoleExistsAsync(name))
        {
            TempData["Error"] = "این نقش از قبل وجود دارد.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _roles.CreateAsync(new IdentityRole(name));
        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(" · ", result.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Index));
        }

        await _audit.LogAsync("role.create", "Role", name, name, HttpContext);
        TempData["Saved"] = $"نقش «{name}» ساخته شد. اکنون مجوزها را تنظیم کنید.";
        return RedirectToAction(nameof(Permissions), new { role = name });
    }

    [HttpGet]
    public async Task<IActionResult> Permissions(string role)
    {
        if (string.IsNullOrWhiteSpace(role) || !await _roles.RoleExistsAsync(role))
            return NotFound();

        var identityRole = await _roles.FindByNameAsync(role);
        if (identityRole is null) return NotFound();

        var claims = await _roles.GetClaimsAsync(identityRole);
        var pages = claims.Where(c => c.Type == AppClaims.Page).Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var caps = claims.Where(c => c.Type != AppClaims.Page && c.Value == "true")
            .Select(c => c.Type).ToHashSet(StringComparer.OrdinalIgnoreCase);

        ViewData["Title"] = $"مجوزهای نقش · {role}";
        return View(new RolePermissionsVm
        {
            RoleName = role,
            IsBuiltIn = AppRoles.IsBuiltIn(role),
            IsSuperAdmin = string.Equals(role, AppRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase),
            PageTree = PermissionCatalog.GetPageTree(),
            Capabilities = PermissionCatalog.GetCapabilities(),
            SelectedPages = pages,
            SelectedCapabilities = caps
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePermissions(string role, string[]? pages, string[]? capabilities)
    {
        if (string.IsNullOrWhiteSpace(role))
            return NotFound();

        var identityRole = await _roles.FindByNameAsync(role);
        if (identityRole is null) return NotFound();

        // SuperAdmin is always full access — claims optional but we still allow save for visibility
        var existing = await _roles.GetClaimsAsync(identityRole);
        foreach (var c in existing.Where(c => c.Type == AppClaims.Page || AppClaims.Capabilities.Any(x => x.Type == c.Type)))
            await _roles.RemoveClaimAsync(identityRole, c);

        var validPageKeys = AdminNavCatalog.All.Select(i => i.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var p in (pages ?? Array.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (validPageKeys.Contains(p))
                await _roles.AddClaimAsync(identityRole, new Claim(AppClaims.Page, p));
        }

        var validCaps = AppClaims.Capabilities.Select(c => c.Type).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var cap in (capabilities ?? Array.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (validCaps.Contains(cap))
                await _roles.AddClaimAsync(identityRole, new Claim(cap, "true"));
        }

        await _audit.LogAsync("role.permissions", "Role", role,
            $"pages={(pages?.Length ?? 0)} caps={(capabilities?.Length ?? 0)}", HttpContext);

        TempData["Saved"] = $"مجوزهای نقش «{role}» ذخیره شد. کاربران باید دوباره وارد شوند تا توکن تازه‌سازی شود.";
        return RedirectToAction(nameof(Permissions), new { role });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string role)
    {
        if (string.IsNullOrWhiteSpace(role) || AppRoles.IsBuiltIn(role))
        {
            TempData["Error"] = "نقش‌های سیستمی قابل حذف نیستند.";
            return RedirectToAction(nameof(Index));
        }

        var identityRole = await _roles.FindByNameAsync(role);
        if (identityRole is null) return NotFound();

        var inRole = await _users.GetUsersInRoleAsync(role);
        foreach (var u in inRole)
            await _users.RemoveFromRoleAsync(u, role);

        await _roles.DeleteAsync(identityRole);
        await _audit.LogAsync("role.delete", "Role", role, role, HttpContext);
        TempData["Saved"] = $"نقش «{role}» حذف شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Assign(string? userId = null)
    {
        ViewData["Title"] = "اختصاص نقش به کاربر";
        var roles = await _roles.Roles.OrderBy(r => r.Name).Select(r => r.Name!).ToListAsync();
        var users = await _users.Users.AsNoTracking()
            .OrderBy(u => u.DisplayName)
            .Select(u => new { u.Id, u.DisplayName, u.UserName, u.Email })
            .Take(500)
            .ToListAsync();

        IList<string> current = Array.Empty<string>();
        if (!string.IsNullOrEmpty(userId))
        {
            var u = await _users.FindByIdAsync(userId);
            if (u != null) current = await _users.GetRolesAsync(u);
        }

        return View(new AssignRoleVm
        {
            UserId = userId,
            AllRoles = roles,
            Users = users.Select(u => new AssignUserRow
            {
                Id = u.Id,
                DisplayName = u.DisplayName,
                UserName = u.UserName ?? "",
                Email = u.Email ?? ""
            }).ToList(),
            CurrentRoles = current.ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(string userId, string[]? roles)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user is null) return NotFound();

        if (user.Id == AuthorAccess.UserId(User))
        {
            TempData["Error"] = "نمی‌توانید نقش‌های خودتان را از اینجا تغییر دهید.";
            return RedirectToAction(nameof(Assign), new { userId });
        }

        var selected = (roles ?? Array.Empty<string>())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var allRoleNames = await _roles.Roles.Select(r => r.Name!).ToListAsync();
        selected = selected.Where(r => allRoleNames.Contains(r, StringComparer.OrdinalIgnoreCase)).ToList();

        var current = await _users.GetRolesAsync(user);
        var toRemove = current.Where(r => !selected.Contains(r, StringComparer.OrdinalIgnoreCase)).ToList();
        var toAdd = selected.Where(r => !current.Contains(r, StringComparer.OrdinalIgnoreCase)).ToList();

        if (toRemove.Count > 0) await _users.RemoveFromRolesAsync(user, toRemove);
        if (toAdd.Count > 0) await _users.AddToRolesAsync(user, toAdd);

        await _audit.LogAsync("user.set_roles", "User", userId,
            $"{user.UserName} → [{string.Join(", ", selected)}]", HttpContext);

        TempData["Saved"] = $"نقش‌های «{user.DisplayName}» به‌روز شد.";
        return RedirectToAction(nameof(Assign), new { userId });
    }
}

public sealed class RoleListItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsBuiltIn { get; set; }
    public int PageClaimCount { get; set; }
    public int CapabilityCount { get; set; }
    public int UserCount { get; set; }
}

public sealed class RolePermissionsVm
{
    public string RoleName { get; set; } = "";
    public bool IsBuiltIn { get; set; }
    public bool IsSuperAdmin { get; set; }
    public IReadOnlyList<PermissionCatalog.GroupNode> PageTree { get; set; } = Array.Empty<PermissionCatalog.GroupNode>();
    public IReadOnlyList<PermissionCatalog.CapabilityNode> Capabilities { get; set; } = Array.Empty<PermissionCatalog.CapabilityNode>();
    public HashSet<string> SelectedPages { get; set; } = new();
    public HashSet<string> SelectedCapabilities { get; set; } = new();
}

public sealed class AssignRoleVm
{
    public string? UserId { get; set; }
    public List<string> AllRoles { get; set; } = new();
    public List<AssignUserRow> Users { get; set; } = new();
    public List<string> CurrentRoles { get; set; } = new();
}

public sealed class AssignUserRow
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Email { get; set; } = "";
}
