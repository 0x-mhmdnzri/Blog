using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

/// <summary>Background job queue + dead-letter (failed email / jobs) management.</summary>
[Authorize(Roles = AppRoles.SuperAdmin)]
public class AdminBackgroundJobsController : Controller
{
    private readonly ApplicationDbContext _db;

    public AdminBackgroundJobsController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index(string? status = "dead", string? type = null)
    {
        ViewData["Title"] = "صف کارها";
        status = (status ?? "dead").Trim().ToLowerInvariant();
        type = string.IsNullOrWhiteSpace(type) ? null : type.Trim();

        var q = _db.BackgroundJobs.AsNoTracking().AsQueryable();
        q = status switch
        {
            "pending" => q.Where(j => j.Status == BackgroundJobStatus.Pending),
            "running" => q.Where(j => j.Status == BackgroundJobStatus.Running),
            "succeeded" => q.Where(j => j.Status == BackgroundJobStatus.Succeeded),
            "failed" or "dead" => q.Where(j => j.Status == BackgroundJobStatus.Failed),
            "all" => q,
            _ => q.Where(j => j.Status == BackgroundJobStatus.Failed)
        };
        if (type is not null)
            q = q.Where(j => j.Type == type);

        var items = await q.OrderByDescending(j => j.Id).Take(200).ToListAsync();

        ViewBag.Status = status;
        ViewBag.Type = type;
        ViewBag.Pending = await _db.BackgroundJobs.CountAsync(j => j.Status == BackgroundJobStatus.Pending);
        ViewBag.Failed = await _db.BackgroundJobs.CountAsync(j => j.Status == BackgroundJobStatus.Failed);
        ViewBag.Succeeded = await _db.BackgroundJobs.CountAsync(j => j.Status == BackgroundJobStatus.Succeeded);
        ViewBag.Running = await _db.BackgroundJobs.CountAsync(j => j.Status == BackgroundJobStatus.Running);
        ViewBag.EmailFailed = await _db.BackgroundJobs.CountAsync(j =>
            j.Status == BackgroundJobStatus.Failed && j.Type == BackgroundJobTypes.SendEmail);

        return View(items);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Retry(long id)
    {
        var job = await _db.BackgroundJobs.AsTracking().FirstOrDefaultAsync(j => j.Id == id);
        if (job is null) return NotFound();

        job.Status = BackgroundJobStatus.Pending;
        job.Attempts = 0;
        job.AvailableAtUtc = null;
        job.StartedAtUtc = null;
        job.CompletedAtUtc = null;
        job.LastError = null;
        await _db.SaveChangesAsync();
        TempData["JobOk"] = $"کار #{id} دوباره در صف قرار گرفت.";
        return RedirectToAction(nameof(Index), new { status = "pending" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RetryAllFailed(string? type = null)
    {
        var q = _db.BackgroundJobs.AsTracking()
            .Where(j => j.Status == BackgroundJobStatus.Failed);
        if (!string.IsNullOrWhiteSpace(type))
            q = q.Where(j => j.Type == type);

        var list = await q.Take(500).ToListAsync();
        foreach (var job in list)
        {
            job.Status = BackgroundJobStatus.Pending;
            job.Attempts = 0;
            job.AvailableAtUtc = null;
            job.StartedAtUtc = null;
            job.CompletedAtUtc = null;
            job.LastError = null;
        }
        await _db.SaveChangesAsync();
        TempData["JobOk"] = $"{list.Count} کار ناموفق دوباره صف شدند.";
        return RedirectToAction(nameof(Index), new { status = "pending", type });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(long id)
    {
        var job = await _db.BackgroundJobs.FindAsync(id);
        if (job is null) return NotFound();
        _db.BackgroundJobs.Remove(job);
        await _db.SaveChangesAsync();
        TempData["JobOk"] = $"کار #{id} حذف شد.";
        return RedirectToAction(nameof(Index), new { status = "dead" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PurgeSucceeded()
    {
        var cut = DateTime.UtcNow.AddDays(-7);
        var n = await _db.BackgroundJobs
            .Where(j => j.Status == BackgroundJobStatus.Succeeded && j.CompletedAtUtc < cut)
            .ExecuteDeleteAsync();
        TempData["JobOk"] = $"{n} کار موفق قدیمی پاک شد.";
        return RedirectToAction(nameof(Index), new { status = "succeeded" });
    }
}
