using BlogApp.Api.Auth;
using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

[Authorize(Roles = AppRoles.SuperAdmin)]
public class AdminApiKeysController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public AdminApiKeysController(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q = null, string? status = null)
    {
        ViewData["Title"] = "API Keys";
        var query = _db.ApiKeys.AsNoTracking().Include(k => k.User).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            if (q.Length > 80) q = q[..80];
            query = query.Where(k =>
                k.Name.Contains(q)
                || k.KeyPrefix.Contains(q)
                || (k.User != null && k.User.UserName != null && k.User.UserName.Contains(q)));
        }

        status = status?.ToLowerInvariant();
        query = status switch
        {
            "banned" => query.Where(k => k.IsBanned),
            "disabled" => query.Where(k => !k.IsActive && !k.IsBanned),
            "active" => query.Where(k => k.IsActive && !k.IsBanned),
            _ => query
        };

        var list = await query.OrderByDescending(k => k.CreatedAtUtc).Take(200).ToListAsync();
        ViewBag.Status = status;
        ViewBag.Q = q;
        return View(list);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Disable(int id)
    {
        var key = await _db.ApiKeys.AsTracking().FirstOrDefaultAsync(k => k.Id == id);
        if (key is null) return NotFound();
        key.IsActive = false;
        await _db.SaveChangesAsync();
        await _audit.LogAsync(User, "api_key.disable", $"ApiKeyId={id}");
        TempData["Msg"] = "کلید غیرفعال شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Enable(int id)
    {
        var key = await _db.ApiKeys.AsTracking().FirstOrDefaultAsync(k => k.Id == id);
        if (key is null) return NotFound();
        if (key.IsBanned)
        {
            TempData["Msg"] = "کلید بن‌شده است؛ ابتدا رفع بن کنید.";
            return RedirectToAction(nameof(Index));
        }
        key.IsActive = true;
        await _db.SaveChangesAsync();
        await _audit.LogAsync(User, "api_key.enable", $"ApiKeyId={id}");
        TempData["Msg"] = "کلید فعال شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Ban(int id, string? reason)
    {
        var key = await _db.ApiKeys.AsTracking().FirstOrDefaultAsync(k => k.Id == id);
        if (key is null) return NotFound();
        key.IsBanned = true;
        key.IsActive = false;
        key.BannedAtUtc = DateTime.UtcNow;
        key.BanReason = string.IsNullOrWhiteSpace(reason) ? "Banned by admin" : reason.Trim()[..Math.Min(500, reason.Trim().Length)];
        await _db.SaveChangesAsync();
        await _audit.LogAsync(User, "api_key.ban", $"ApiKeyId={id} Reason={key.BanReason}");
        TempData["Msg"] = "کلید بن شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Unban(int id)
    {
        var key = await _db.ApiKeys.AsTracking().FirstOrDefaultAsync(k => k.Id == id);
        if (key is null) return NotFound();
        key.IsBanned = false;
        key.BanReason = null;
        key.BannedAtUtc = null;
        key.AbuseStrikeCount = 0;
        key.IsActive = true;
        await _db.SaveChangesAsync();
        await _audit.LogAsync(User, "api_key.unban", $"ApiKeyId={id}");
        TempData["Msg"] = "رفع بن انجام شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var key = await _db.ApiKeys.AsTracking().FirstOrDefaultAsync(k => k.Id == id);
        if (key is null) return NotFound();
        _db.ApiKeys.Remove(key);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(User, "api_key.delete", $"ApiKeyId={id}");
        TempData["Msg"] = "کلید حذف شد.";
        return RedirectToAction(nameof(Index));
    }
}
