using AVICRM.Data;
using AVICRM.Models;
using AVICRM.Services;
using AVICRM.Services.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AVICRM.Controllers;

[Authorize(Roles = AppRoles.SuperAdmin)]
public class AdminApiKeysController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly INotificationService _notify;

    public AdminApiKeysController(ApplicationDbContext db, IAuditService audit, INotificationService notify)
    {
        _db = db;
        _audit = audit;
        _notify = notify;
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
            "pending" => query.Where(k => k.ApprovalStatus == ApiKeyApprovalStatus.Pending && !k.IsBanned),
            "approved" => query.Where(k => k.ApprovalStatus == ApiKeyApprovalStatus.Approved && k.IsActive && !k.IsBanned),
            "rejected" => query.Where(k => k.ApprovalStatus == ApiKeyApprovalStatus.Rejected),
            "banned" => query.Where(k => k.IsBanned),
            "disabled" => query.Where(k => !k.IsActive && !k.IsBanned),
            "active" => query.Where(k => k.IsActive && !k.IsBanned && k.ApprovalStatus == ApiKeyApprovalStatus.Approved),
            _ => query
        };

        var list = await query.OrderByDescending(k => k.CreatedAtUtc).Take(200).ToListAsync();
        ViewBag.Status = status;
        ViewBag.Q = q;
        ViewBag.PendingCount = await _db.ApiKeys.AsNoTracking()
            .CountAsync(k => k.ApprovalStatus == ApiKeyApprovalStatus.Pending && !k.IsBanned);
        return View(list);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var key = await _db.ApiKeys.AsTracking().FirstOrDefaultAsync(k => k.Id == id);
        if (key is null) return NotFound();
        if (key.IsBanned)
        {
            TempData["Msg"] = "کلید بن‌شده است؛ ابتدا رفع بن کنید.";
            return RedirectToAction(nameof(Index));
        }

        var adminId = AuthorAccess.UserId(User);
        key.ApprovalStatus = ApiKeyApprovalStatus.Approved;
        key.ApprovedAtUtc = DateTime.UtcNow;
        key.ApprovedByUserId = adminId;
        key.RejectionReason = null;
        key.IsActive = true;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("api_key.approve", "ApiKey", id.ToString(), http: HttpContext);

        try
        {
            await _notify.NotifyAsync(
                key.UserId,
                NotificationKind.System,
                "کلید API شما تأیید شد",
                $"PAT «{key.Name}» ({key.KeyPrefix}…) توسط مالک تأیید شد. از این لحظه می‌توانید با همان توکنی که هنگام درخواست کپی کردید API را فراخوانی کنید.",
                "/AccountApiKeys");
        }
        catch { /* non-fatal */ }

        TempData["Msg"] = "کلید تأیید شد و به کاربر اطلاع داده شد.";
        return RedirectToAction(nameof(Index), new { status = "pending" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? reason)
    {
        var key = await _db.ApiKeys.AsTracking().FirstOrDefaultAsync(k => k.Id == id);
        if (key is null) return NotFound();

        var r = string.IsNullOrWhiteSpace(reason) ? "رد توسط سوپرادمین" : reason.Trim();
        if (r.Length > 500) r = r[..500];

        key.ApprovalStatus = ApiKeyApprovalStatus.Rejected;
        key.IsActive = false;
        key.RejectionReason = r;
        key.ApprovedAtUtc = null;
        key.ApprovedByUserId = AuthorAccess.UserId(User);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("api_key.reject", "ApiKey", id.ToString(), r, HttpContext);

        try
        {
            await _notify.NotifyAsync(
                key.UserId,
                NotificationKind.System,
                "درخواست کلید API رد شد",
                $"PAT «{key.Name}» رد شد. دلیل: {r}",
                "/AccountApiKeys");
        }
        catch { /* non-fatal */ }

        TempData["Msg"] = "درخواست رد شد و به کاربر اطلاع داده شد.";
        return RedirectToAction(nameof(Index), new { status = "pending" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Disable(int id)
    {
        var key = await _db.ApiKeys.AsTracking().FirstOrDefaultAsync(k => k.Id == id);
        if (key is null) return NotFound();
        key.IsActive = false;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("api_key.disable", "ApiKey", id.ToString(), http: HttpContext);
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
        if (key.ApprovalStatus != ApiKeyApprovalStatus.Approved)
        {
            TempData["Msg"] = "ابتدا کلید را تأیید کنید.";
            return RedirectToAction(nameof(Index));
        }
        key.IsActive = true;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("api_key.enable", "ApiKey", id.ToString(), http: HttpContext);
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
        var r = string.IsNullOrWhiteSpace(reason) ? "Banned by admin" : reason.Trim();
        key.BanReason = r.Length > 500 ? r[..500] : r;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("api_key.ban", "ApiKey", id.ToString(), key.BanReason, HttpContext);
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
        if (key.ApprovalStatus == ApiKeyApprovalStatus.Approved)
            key.IsActive = true;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("api_key.unban", "ApiKey", id.ToString(), http: HttpContext);
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
        await _audit.LogAsync("api_key.delete", "ApiKey", id.ToString(), http: HttpContext);
        TempData["Msg"] = "کلید حذف شد.";
        return RedirectToAction(nameof(Index));
    }
}
