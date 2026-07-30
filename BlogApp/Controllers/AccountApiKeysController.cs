using BlogApp.Api.Auth;
using BlogApp.Api.Dtos;
using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using BlogApp.Services.Messaging;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

/// <summary>User-facing PAT management — any authenticated user can request keys; SuperAdmin approves.</summary>
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
    public async Task<IActionResult> Index(int range = 30)
    {
        if (range is not (7 or 30 or 90)) range = 30;

        ViewData["Title"] = "کلیدهای API";
        // Use admin shell when user is staff so sidebar shows «کلیدهای API من»
        var isStaff = AuthorAccess.IsSuperAdmin(User) || User.IsInRole(AppRoles.Author);
        if (isStaff)
            ViewData["UseAdminLayout"] = true;

        var userId = AuthorAccess.UserId(User)!;
        var keys = await _db.ApiKeys.AsNoTracking()
            .Where(k => k.UserId == userId)
            .OrderByDescending(k => k.CreatedAtUtc)
            .ToListAsync();

        var usage = await BuildSelfUsageAsync(userId, range);

        return View(new AccountApiKeysPageModel
        {
            Keys = keys,
            Usage = usage,
            RangeDays = range
        });
    }

    private async Task<ApiSelfUsage> BuildSelfUsageAsync(string userId, int range)
    {
        var today = DateTime.UtcNow.Date;
        var rangeStart = today.AddDays(-(range - 1));

        List<ApiRequestLog> logs;
        try
        {
            logs = await _db.ApiRequestLogs.AsNoTracking()
                .Where(l => l.UserId == userId && l.CreatedAtUtc >= rangeStart)
                .OrderByDescending(l => l.CreatedAtUtc)
                .Take(5_000)
                .ToListAsync();
        }
        catch
        {
            logs = new List<ApiRequestLog>();
        }

        var byDay = new List<ChartPoint>();
        for (var d = rangeStart; d <= today; d = d.AddDays(1))
        {
            byDay.Add(new ChartPoint
            {
                Label = d.ToString("MM-dd"),
                Value = logs.Count(l => l.CreatedAtUtc.Date == d)
            });
        }

        var endpoints = logs
            .GroupBy(l => l.Method + " " + l.Path)
            .Select(g => new ApiEndpointUsageRow
            {
                Method = g.First().Method,
                Path = g.First().Path,
                Count = g.Count(),
                Errors = g.Count(x => x.IsError),
                AvgMs = Math.Round(g.Average(x => x.DurationMs), 1)
            })
            .OrderByDescending(e => e.Count)
            .Take(12)
            .ToList();

        return new ApiSelfUsage
        {
            TotalRequests = logs.Count,
            ErrorCount = logs.Count(l => l.IsError),
            RateLimitedCount = logs.Count(l => l.IsRateLimited),
            AvgDurationMs = logs.Count == 0 ? 0 : Math.Round(logs.Average(l => l.DurationMs), 1),
            RequestsByDay = byDay,
            TopEndpoints = endpoints,
            Recent = logs.Take(25).Select(l => new ApiRecentCall
            {
                Method = l.Method,
                Path = l.Path,
                StatusCode = l.StatusCode,
                DurationMs = l.DurationMs,
                CreatedAtUtc = l.CreatedAtUtc,
                KeyPrefix = l.KeyPrefix
            }).ToList()
        };
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

public class AccountApiKeysPageModel
{
    public List<ApiKey> Keys { get; set; } = new();
    public ApiSelfUsage Usage { get; set; } = new();
    public int RangeDays { get; set; } = 30;
}

public class ApiSelfUsage
{
    public int TotalRequests { get; set; }
    public int ErrorCount { get; set; }
    public int RateLimitedCount { get; set; }
    public double AvgDurationMs { get; set; }
    public List<ChartPoint> RequestsByDay { get; set; } = new();
    public List<ApiEndpointUsageRow> TopEndpoints { get; set; } = new();
    public List<ApiRecentCall> Recent { get; set; } = new();
}

public class ApiRecentCall
{
    public string Method { get; set; } = "";
    public string Path { get; set; } = "";
    public int StatusCode { get; set; }
    public int DurationMs { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? KeyPrefix { get; set; }
}
