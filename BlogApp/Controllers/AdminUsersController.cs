using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

[Authorize(Roles = AppRoles.SuperAdmin)]
public class AdminUsersController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public AdminUsersController(
        UserManager<ApplicationUser> users,
        ApplicationDbContext db,
        IAuditService audit)
    {
        _users = users;
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q = null, string? role = null)
    {
        ViewData["Title"] = "مدیریت کاربران";
        ViewBag.Query = q;
        ViewBag.RoleFilter = role;

        var list = await _users.Users.AsNoTracking()
            .OrderByDescending(u => u.CreatedAtUtc)
            .ToListAsync();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            list = list.Where(u =>
                (u.UserName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || (u.Email?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || u.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var items = new List<AdminUserListItem>();
        foreach (var u in list)
        {
            var roles = await _users.GetRolesAsync(u);
            if (!string.IsNullOrWhiteSpace(role) && !roles.Contains(role))
                continue;

            items.Add(new AdminUserListItem
            {
                Id = u.Id,
                UserName = u.UserName ?? "",
                Email = u.Email,
                DisplayName = u.DisplayName,
                Roles = roles.ToList(),
                IsLockedOut = u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow,
                LockoutEnd = u.LockoutEnd,
                CreatedAtUtc = u.CreatedAtUtc,
                PostCount = await _db.Posts.CountAsync(p => p.AuthorId == u.Id),
                EmailConfirmed = u.EmailConfirmed
            });
        }

        return View(items);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRole(string userId, string role)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user is null) return NotFound();

        var selfId = AuthorAccess.UserId(User);
        if (user.Id == selfId)
        {
            TempData["Error"] = "نمی‌توانید نقش خودتان را تغییر دهید.";
            return RedirectToAction(nameof(Index));
        }

        role = role?.Trim() ?? "";
        if (role is not (AppRoles.Reader or AppRoles.Author or AppRoles.SuperAdmin))
        {
            TempData["Error"] = "نقش نامعتبر است.";
            return RedirectToAction(nameof(Index));
        }

        var current = await _users.GetRolesAsync(user);
        await _users.RemoveFromRolesAsync(user, current);

        // Always keep Reader for bookmark capability when Author/SuperAdmin
        if (role == AppRoles.SuperAdmin)
        {
            await _users.AddToRolesAsync(user, new[] { AppRoles.SuperAdmin, AppRoles.Author, AppRoles.Reader });
        }
        else if (role == AppRoles.Author)
        {
            await _users.AddToRolesAsync(user, new[] { AppRoles.Author, AppRoles.Reader });
        }
        else
        {
            await _users.AddToRoleAsync(user, AppRoles.Reader);
        }

        await _audit.LogAsync("user.set_role", "User", userId,
            $"{user.UserName} → {role}", HttpContext);

        TempData["Saved"] = $"نقش «{user.DisplayName}» به {role} تغییر کرد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLock(string userId)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user is null) return NotFound();

        if (user.Id == AuthorAccess.UserId(User))
        {
            TempData["Error"] = "نمی‌توانید حساب خودتان را قفل کنید.";
            return RedirectToAction(nameof(Index));
        }

        var locked = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;
        if (locked)
        {
            user.LockoutEnd = null;
            await _users.UpdateAsync(user);
            await _audit.LogAsync("user.unlock", "User", userId, user.UserName, HttpContext);
            TempData["Saved"] = $"قفل حساب «{user.DisplayName}» برداشته شد.";
        }
        else
        {
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
            await _users.UpdateAsync(user);
            await _audit.LogAsync("user.lock", "User", userId, user.UserName, HttpContext);
            TempData["Saved"] = $"حساب «{user.DisplayName}» قفل شد.";
        }

        return RedirectToAction(nameof(Index));
    }
}
