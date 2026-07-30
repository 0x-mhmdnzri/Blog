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
    public IActionResult Index()
    {
        ViewData["Title"] = "مدیریت کاربران";
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Data(string? role = null)
    {
        var req = DataTablesRequest.From(Request);

        // Materialize users — Identity roles need per-user lookups
        var all = await _users.Users.AsNoTracking().ToListAsync();
        var total = all.Count;

        var enriched = new List<(ApplicationUser U, List<string> Roles, int PostCount, bool Locked)>();
        foreach (var u in all)
        {
            var roles = (await _users.GetRolesAsync(u)).ToList();
            if (!string.IsNullOrWhiteSpace(role) && !roles.Contains(role))
                continue;
            var posts = await _db.Posts.CountAsync(p => p.AuthorId == u.Id);
            var locked = u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow;
            enriched.Add((u, roles, posts, locked));
        }

        if (!string.IsNullOrWhiteSpace(req.SearchValue))
        {
            var term = req.SearchValue;
            enriched = enriched.Where(x =>
                (x.U.UserName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || (x.U.Email?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || x.U.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.Roles.Any(r => r.Contains(term, StringComparison.OrdinalIgnoreCase))).ToList();
        }

        var filtered = enriched.Count;

        // 0 #, 1 user, 2 roles, 3 posts, 4 created, 5 status, 6 actions
        IEnumerable<(ApplicationUser U, List<string> Roles, int PostCount, bool Locked)> ordered = (req.OrderColumn, req.Asc) switch
        {
            (1, true) => enriched.OrderBy(x => x.U.DisplayName),
            (1, false) => enriched.OrderByDescending(x => x.U.DisplayName),
            (2, true) => enriched.OrderBy(x => string.Join(",", x.Roles)),
            (2, false) => enriched.OrderByDescending(x => string.Join(",", x.Roles)),
            (3, true) => enriched.OrderBy(x => x.PostCount),
            (3, false) => enriched.OrderByDescending(x => x.PostCount),
            (4, true) => enriched.OrderBy(x => x.U.CreatedAtUtc),
            (4, false) => enriched.OrderByDescending(x => x.U.CreatedAtUtc),
            (5, true) => enriched.OrderBy(x => x.Locked),
            (5, false) => enriched.OrderByDescending(x => x.Locked),
            _ => enriched.OrderByDescending(x => x.U.CreatedAtUtc)
        };

        var page = ordered.Skip(req.Start).Take(req.Length).ToList();
        var af = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>();
        var token = af.GetAndStoreTokens(HttpContext).RequestToken ?? "";

        var rows = page.Select((x, i) =>
        {
            var userHtml = $"<div dir=\"auto\"><strong>{System.Net.WebUtility.HtmlEncode(x.U.DisplayName)}</strong></div>" +
                           $"<div class=\"small ltr-field text-muted-dark\">{System.Net.WebUtility.HtmlEncode(x.U.UserName)} · {System.Net.WebUtility.HtmlEncode(x.U.Email)}</div>";
            var statusHtml = x.Locked
                ? "<span class=\"status-pill rejected\">قفل</span>"
                : "<span class=\"status-pill approved\">فعال</span>";
            var actions =
                $"<div class=\"d-flex flex-wrap gap-1 align-items-center\">" +
                $"<form method=\"post\" action=\"/AdminUsers/SetRole\" class=\"d-inline-flex gap-1 align-items-center\">" +
                $"<input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"{token}\" />" +
                $"<input type=\"hidden\" name=\"userId\" value=\"{x.U.Id}\" />" +
                "<select name=\"role\" class=\"form-select form-select-sm\" style=\"width:auto;\">" +
                "<option value=\"Reader\">Reader</option><option value=\"Author\">Author</option><option value=\"SuperAdmin\">SuperAdmin</option>" +
                "</select><button type=\"submit\" class=\"icon-btn\">نقش</button></form>" +
                $"<form method=\"post\" action=\"/AdminUsers/ToggleLock\">" +
                $"<input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"{token}\" />" +
                $"<input type=\"hidden\" name=\"userId\" value=\"{x.U.Id}\" />" +
                $"<button type=\"submit\" class=\"icon-btn {(x.Locked ? "approve" : "reject")}\">{(x.Locked ? "باز کردن" : "قفل")}</button></form></div>";

            return new object[]
            {
                req.Start + i + 1,
                userHtml,
                System.Net.WebUtility.HtmlEncode(string.Join(", ", x.Roles)),
                x.PostCount,
                PersianDate.Date(x.U.CreatedAtUtc),
                statusHtml,
                actions
            };
        }).ToList();

        return Json(DataTablesResponse.Ok(req.Draw, total, filtered, rows));
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

        if (role == AppRoles.SuperAdmin)
            await _users.AddToRolesAsync(user, new[] { AppRoles.SuperAdmin, AppRoles.Author, AppRoles.Reader });
        else if (role == AppRoles.Author)
            await _users.AddToRolesAsync(user, new[] { AppRoles.Author, AppRoles.Reader });
        else
            await _users.AddToRoleAsync(user, AppRoles.Reader);

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
