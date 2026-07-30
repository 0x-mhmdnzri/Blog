using BlogApp.Api.Auth;
using BlogApp.Api.Dtos;
using BlogApp.Api.Validation;
using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

/// <summary>User-facing PAT management (GitHub-style personal access tokens).</summary>
[Authorize]
public class AccountApiKeysController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IValidator<ApiKeyCreateDto> _validator;

    public AccountApiKeysController(ApplicationDbContext db, IValidator<ApiKeyCreateDto> validator)
    {
        _db = db;
        _validator = validator;
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
        var existing = await _db.ApiKeys.CountAsync(k => k.UserId == userId && k.IsActive && !k.IsBanned);
        if (existing >= 10)
        {
            TempData["Err"] = "حداکثر ۱۰ کلید فعال مجاز است.";
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
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = expiresInDays is > 0 ? DateTime.UtcNow.AddDays(expiresInDays.Value) : null
        };

        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        _db.ApiKeys.Add(key);
        await _db.SaveChangesAsync();

        TempData["NewToken"] = token;
        TempData["Msg"] = "کلید ساخته شد. فقط یک‌بار نمایش داده می‌شود — کپی کنید.";
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
