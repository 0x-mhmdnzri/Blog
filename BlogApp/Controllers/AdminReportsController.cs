using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

[Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
public class AdminReportsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public AdminReportsController(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string status = "open")
    {
        ViewData["Title"] = "گزارش‌های محتوا";
        ViewBag.CurrentStatus = status;

        var query = _db.ContentReports.AsNoTracking().AsQueryable();
        query = status switch
        {
            "resolved" => query.Where(r => r.Status == ContentReportStatus.Resolved),
            "dismissed" => query.Where(r => r.Status == ContentReportStatus.Dismissed),
            "all" => query,
            _ => query.Where(r => r.Status == ContentReportStatus.Open)
        };

        // Authors only see reports on their own posts/comments
        if (!AuthorAccess.IsSuperAdmin(User))
        {
            var uid = AuthorAccess.UserId(User)!;
            var myPostIds = await _db.Posts.Where(p => p.AuthorId == uid).Select(p => p.Id).ToListAsync();
            var myCommentIds = await _db.Comments.Where(c => myPostIds.Contains(c.PostId)).Select(c => c.Id).ToListAsync();
            query = query.Where(r =>
                (r.TargetType == ContentReportTarget.Post && myPostIds.Contains(r.TargetId))
                || (r.TargetType == ContentReportTarget.Comment && myCommentIds.Contains(r.TargetId)));
        }

        var items = await query.OrderByDescending(r => r.CreatedAtUtc).Take(200).ToListAsync();

        ViewBag.OpenCount = await CountForScope(ContentReportStatus.Open);
        ViewBag.ResolvedCount = await CountForScope(ContentReportStatus.Resolved);
        ViewBag.DismissedCount = await CountForScope(ContentReportStatus.Dismissed);

        return View(items);
    }

    private async Task<int> CountForScope(ContentReportStatus status)
    {
        var q = _db.ContentReports.AsNoTracking().Where(r => r.Status == status);
        if (!AuthorAccess.IsSuperAdmin(User))
        {
            var uid = AuthorAccess.UserId(User)!;
            var myPostIds = await _db.Posts.Where(p => p.AuthorId == uid).Select(p => p.Id).ToListAsync();
            var myCommentIds = await _db.Comments.Where(c => myPostIds.Contains(c.PostId)).Select(c => c.Id).ToListAsync();
            q = q.Where(r =>
                (r.TargetType == ContentReportTarget.Post && myPostIds.Contains(r.TargetId))
                || (r.TargetType == ContentReportTarget.Comment && myCommentIds.Contains(r.TargetId)));
        }
        return await q.CountAsync();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(int id, string? returnStatus)
    {
        var report = await _db.ContentReports.FindAsync(id);
        if (report is null) return NotFound();
        if (!await CanManageReport(report)) return Forbid();

        report.Status = ContentReportStatus.Resolved;
        report.ResolvedAtUtc = DateTime.UtcNow;
        report.ResolvedByUserId = AuthorAccess.UserId(User);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("report.resolve", "ContentReport", id.ToString(), report.Reason, HttpContext);

        return RedirectToAction(nameof(Index), new { status = returnStatus ?? "open" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Dismiss(int id, string? returnStatus)
    {
        var report = await _db.ContentReports.FindAsync(id);
        if (report is null) return NotFound();
        if (!await CanManageReport(report)) return Forbid();

        report.Status = ContentReportStatus.Dismissed;
        report.ResolvedAtUtc = DateTime.UtcNow;
        report.ResolvedByUserId = AuthorAccess.UserId(User);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("report.dismiss", "ContentReport", id.ToString(), report.Reason, HttpContext);

        return RedirectToAction(nameof(Index), new { status = returnStatus ?? "open" });
    }

    private async Task<bool> CanManageReport(ContentReport report)
    {
        if (AuthorAccess.IsSuperAdmin(User)) return true;
        var uid = AuthorAccess.UserId(User);
        if (uid is null) return false;

        if (report.TargetType == ContentReportTarget.Post)
        {
            var post = await _db.Posts.FindAsync(report.TargetId);
            return post is not null && post.AuthorId == uid;
        }

        var comment = await _db.Comments.Include(c => c.Post).FirstOrDefaultAsync(c => c.Id == report.TargetId);
        return comment is not null && comment.Post.AuthorId == uid;
    }
}
