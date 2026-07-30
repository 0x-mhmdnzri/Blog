using BlogApp.Api.Auth;
using BlogApp.Api.Dtos;
using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services;
using BlogApp.Services.Messaging;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

/// <summary>User-facing PAT management — any authenticated reader/author can request keys; SuperAdmin approves.</summary>
[Authorize]
public class AccountApiKeysController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IValidator<ApiKeyCreateDto> _validator;
    private readonly INotificationService _notify;

    public AccountApiKeysController(
        ApplicationDbContext db,
        IValidator<ApiKeyCreateDto> validator,
        INotificationService notify)
    {
        _db = db;
        _validator = validator;
        _notify = notify;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "کلیدهای API";
        var userId = AuthorAccess.UserId(User)!;
        var keys = await _db.ApiKeys.AsNoTracking()
            .Where(k => k.UserId == userId)
            .OrderByDescending(k => k.CreatedAtUtc)
            .ToListAsync();
        return View(keys);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string? scopes, int? expiresInDays)
    {
        var dto = new ApiKeyCreateDto(name, scopes, expiresInDays);
        var result = await _validator.ValidateAsync(dto);
        if (!result.IsValid)
        {
            TempData["Err"] = string.Join(" ", result.Errors.Select(e => e.ErrorMessage));
            return RedirectToAction(nameof(Index));
        }

        var userId = AuthorAccess.UserId(User)!;
        var existing = await _db.ApiKeys.CountAsync(k =>
            k.UserId == userId
            && !k.IsBanned
            && k.ApprovalStatus != ApiKeyApprovalStatus.Rejected);
        if (existing >= 10)
        {
            TempData["Err"] = "حداکثر ۱۰ کلید (فعال یا در انتظار تأیید) مجاز است.";
            return RedirectToAction(nameof(Index));
        }

        var (token, prefix, hash) = ApiKeyHasher.Generate();
        var key = new ApiKey
        {
            UserId = userId,
            Name = name.Trim(),
            KeyPrefix = prefix,
            KeyHash = hash,
            Scopes = string.IsNullOrWhiteSpace(scopes) ? ApiScopes.Read : scopes.Trim().ToLowerInvariant(),
            IsActive = true,
            ApprovalStatus = ApiKeyApprovalStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = expiresInDays is > 0 ? DateTime.UtcNow.AddDays(expiresInDays.Value) : null
        };

        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        _db.ApiKeys.Add(key);
        await _db.SaveChangesAsync();

        // Alert SuperAdmins that a new PAT needs review
        try
        {
            var supers = await _db.Users.AsNoTracking()
                .Where(u => _db.UserRoles.Any(ur =>
                    ur.UserId == u.Id
                    && _db.Roles.Any(r => r.Id == ur.RoleId && r.Name == AppRoles.SuperAdmin)))
                .Select(u => u.Id)
                .ToListAsync();

            var uname = User.Identity?.Name ?? userId;
            foreach (var sid in supers)
            {
                await _notify.NotifyAsync(
                    sid,
                    NotificationKind.System,
                    "درخواست کلید API جدید",
                    $"{uname} کلید «{key.Name}» را درخواست کرده — نیاز به تأیید سوپرادمین.",
                    "/AdminApiKeys");
            }
        }
        catch { /* non-fatal */ }

        TempData["NewToken"] = token;
        TempData["Msg"] =
            "کلید ساخته شد و در انتظار تأیید سوپرادمین است. توکن را الان کپی کنید (فقط یک‌بار نمایش داده می‌شود). تا قبل از تأیید، API آن را قبول نمی‌کند.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(int id)
    {
        var userId = AuthorAccess.UserId(User)!;
        var key = await _db.ApiKeys.AsTracking()
            .FirstOrDefaultAsync(k => k.Id == id && k.UserId == userId);
        if (key is null) return NotFound();
        key.IsActive = false;
        await _db.SaveChangesAsync();
        TempData["Msg"] = "کلید باطل شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = AuthorAccess.UserId(User)!;
        var key = await _db.ApiKeys.AsTracking()
            .FirstOrDefaultAsync(k => k.Id == id && k.UserId == userId);
        if (key is null) return NotFound();
        _db.ApiKeys.Remove(key);
        await _db.SaveChangesAsync();
        TempData["Msg"] = "کلید حذف شد.";
        return RedirectToAction(nameof(Index));
    }
}
