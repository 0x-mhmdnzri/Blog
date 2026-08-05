using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using BlogApp.Services.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

[Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
public class AdminReportsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly INotificationService _notify;
    private readonly ILogger<AdminReportsController> _logger;

    public AdminReportsController(
        ApplicationDbContext db,
        IAuditService audit,
        INotificationService notify,
        ILogger<AdminReportsController> logger)
    {
        _db = db;
        _audit = audit;
        _notify = notify;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string status = "open")
    {
        ViewData["Title"] = "گزارش‌های محتوا";
        ViewBag.CurrentStatus = status;
        ViewBag.OpenCount = await CountForScope(ContentReportStatus.Open);
        ViewBag.ResolvedCount = await CountForScope(ContentReportStatus.Resolved);
        ViewBag.DismissedCount = await CountForScope(ContentReportStatus.Dismissed);
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Data(string status = "open")
    {
        var req = DataTablesRequest.From(Request);
        var query = ScopeQuery(_db.ContentReports.AsNoTracking());

        query = status switch
        {
            "resolved" => query.Where(r => r.Status == ContentReportStatus.Resolved),
            "dismissed" => query.Where(r => r.Status == ContentReportStatus.Dismissed),
            "all" => query,
            _ => query.Where(r => r.Status == ContentReportStatus.Open)
        };

        var total = await query.CountAsync();

        if (!string.IsNullOrWhiteSpace(req.SearchValue))
        {
            var term = req.SearchValue;
            query = query.Where(r =>
                r.Reason.Contains(term)
                || (r.TargetTitle != null && r.TargetTitle.Contains(term))
                || (r.ReporterName != null && r.ReporterName.Contains(term))
                || (r.Details != null && r.Details.Contains(term)));
        }

        var filtered = await query.CountAsync();

        // 0 #, 1 target, 2 reason, 3 reporter, 4 date, 5 status, 6 actions
        query = (req.OrderColumn, req.Asc) switch
        {
            (2, true) => query.OrderBy(r => r.Reason),
            (2, false) => query.OrderByDescending(r => r.Reason),
            (3, true) => query.OrderBy(r => r.ReporterName),
            (3, false) => query.OrderByDescending(r => r.ReporterName),
            (4, true) => query.OrderBy(r => r.CreatedAtUtc),
            (4, false) => query.OrderByDescending(r => r.CreatedAtUtc),
            (5, true) => query.OrderBy(r => r.Status),
            (5, false) => query.OrderByDescending(r => r.Status),
            _ => query.OrderByDescending(r => r.CreatedAtUtc)
        };

        var page = await query.Skip(req.Start).Take(req.Length).ToListAsync();
        var af = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>();
        var token = af.GetAndStoreTokens(HttpContext).RequestToken ?? "";

        var rows = page.Select((r, i) =>
        {
            var typeLabel = r.TargetType == ContentReportTarget.Post ? "نوشته" : "دیدگاه";
            var targetHtml =
                $"<span class=\"small text-muted-dark\">{typeLabel} #{r.TargetId}</span>" +
                $"<div dir=\"auto\">{System.Net.WebUtility.HtmlEncode(r.TargetTitle ?? "—")}</div>";
            if (!string.IsNullOrEmpty(r.Details))
                targetHtml += $"<div class=\"small text-muted-dark\" dir=\"auto\">{System.Net.WebUtility.HtmlEncode(r.Details)}</div>";

            var statusHtml = r.Status switch
            {
                ContentReportStatus.Resolved => "<span class=\"status-pill approved\">حل‌شده</span>",
                ContentReportStatus.Dismissed => "<span class=\"status-pill rejected\">ردشده</span>",
                _ => "<span class=\"status-pill pending\">باز</span>"
            };

            var actions = "";
            if (r.Status == ContentReportStatus.Open)
            {
                actions =
                    $"<div class=\"d-flex gap-1\">" +
                    $"<form method=\"post\" action=\"/AdminReports/Resolve\">" +
                    $"<input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"{token}\" />" +
                    $"<input type=\"hidden\" name=\"id\" value=\"{r.Id}\" />" +
                    $"<input type=\"hidden\" name=\"returnStatus\" value=\"{System.Net.WebUtility.HtmlEncode(status)}\" />" +
                    "<button type=\"submit\" class=\"icon-btn approve\">حل</button></form>" +
                    $"<form method=\"post\" action=\"/AdminReports/Dismiss\">" +
                    $"<input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"{token}\" />" +
                    $"<input type=\"hidden\" name=\"id\" value=\"{r.Id}\" />" +
                    $"<input type=\"hidden\" name=\"returnStatus\" value=\"{System.Net.WebUtility.HtmlEncode(status)}\" />" +
                    "<button type=\"submit\" class=\"icon-btn reject\">رد</button></form></div>";
            }

            return new object[]
            {
                req.Start + i + 1,
                targetHtml,
                System.Net.WebUtility.HtmlEncode(r.Reason),
                System.Net.WebUtility.HtmlEncode(r.ReporterName ?? "مهمان"),
                PersianDate.DateTime(r.CreatedAtUtc),
                statusHtml,
                actions
            };
        }).ToList();

        return Json(DataTablesResponse.Ok(req.Draw, total, filtered, rows));
    }

    /// <summary>Export ALL content reports for status + optional search (no page limit).</summary>
    [HttpGet]
    public async Task<IActionResult> ExportCsv(string status = "open", string? search = null)
    {
        var query = ScopeQuery(_db.ContentReports.AsNoTracking());

        query = status switch
        {
            "resolved" => query.Where(r => r.Status == ContentReportStatus.Resolved),
            "dismissed" => query.Where(r => r.Status == ContentReportStatus.Dismissed),
            "all" => query,
            _ => query.Where(r => r.Status == ContentReportStatus.Open)
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(r =>
                r.Reason.Contains(term)
                || (r.TargetTitle != null && r.TargetTitle.Contains(term))
                || (r.ReporterName != null && r.ReporterName.Contains(term))
                || (r.Details != null && r.Details.Contains(term)));
        }

        var list = await query.OrderByDescending(r => r.CreatedAtUtc).ToListAsync();

        var headers = new[]
        {
            "Id", "TargetType", "TargetId", "TargetTitle", "Reason", "Details",
            "ReporterName", "Status", "CreatedAtUtc", "ResolvedAtUtc"
        };

        var rows = list.Select(r => new[]
        {
            CsvExport.Cell(r.Id),
            r.TargetType.ToString(),
            CsvExport.Cell(r.TargetId),
            CsvExport.Cell(r.TargetTitle),
            CsvExport.Cell(r.Reason),
            CsvExport.Cell(r.Details),
            CsvExport.Cell(r.ReporterName),
            r.Status.ToString(),
            CsvExport.Cell(r.CreatedAtUtc),
            CsvExport.Cell(r.ResolvedAtUtc)
        });

        return CsvExport.File($"reports-{status}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv", headers, rows);
    }

    private IQueryable<ContentReport> ScopeQuery(IQueryable<ContentReport> query)
    {
        if (AuthorAccess.IsSuperAdmin(User)) return query;
        var uid = AuthorAccess.UserId(User)!;
        var myPostIds = _db.Posts.Where(p => p.AuthorId == uid).Select(p => p.Id);
        var myCommentIds = _db.Comments.Where(c => myPostIds.Contains(c.PostId)).Select(c => c.Id);
        return query.Where(r =>
            (r.TargetType == ContentReportTarget.Post && myPostIds.Contains(r.TargetId))
            || (r.TargetType == ContentReportTarget.Comment && myCommentIds.Contains(r.TargetId)));
    }

    private async Task<int> CountForScope(ContentReportStatus status)
    {
        return await ScopeQuery(_db.ContentReports.AsNoTracking().Where(r => r.Status == status)).CountAsync();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(int id, string? returnStatus = null, string? returnTo = null)
    {
        // Global EF default is NoTracking — must AsTracking for mutations.
        var report = await _db.ContentReports.AsTracking()
            .FirstOrDefaultAsync(r => r.Id == id);
        if (report is null) return NotFound();
        if (!await CanManageReport(report)) return Forbid();
        if (report.Status != ContentReportStatus.Open)
        {
            TempData["FlashOk"] = "این گزارش قبلاً بسته شده است.";
            return RedirectAfterReportAction(returnStatus, returnTo);
        }

        report.Status = ContentReportStatus.Resolved;
        report.ResolvedAtUtc = DateTime.UtcNow;
        report.ResolvedByUserId = AuthorAccess.UserId(User);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("report.resolve", "ContentReport", id.ToString(), report.Reason, HttpContext);

        await NotifyReporterAsync(
            report,
            title: "گزارش شما رسیدگی شد",
            body: $"گزارش مربوط به «{report.TargetTitle ?? ("#" + report.TargetId)}» بررسی و بسته شد (حل‌شده).");

        TempData["FlashOk"] = "گزارش به‌عنوان رسیدگی‌شده بسته شد.";
        return RedirectAfterReportAction(returnStatus, returnTo);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Dismiss(int id, string? returnStatus = null, string? returnTo = null)
    {
        var report = await _db.ContentReports.AsTracking()
            .FirstOrDefaultAsync(r => r.Id == id);
        if (report is null) return NotFound();
        if (!await CanManageReport(report)) return Forbid();
        if (report.Status != ContentReportStatus.Open)
        {
            TempData["FlashOk"] = "این گزارش قبلاً بسته شده است.";
            return RedirectAfterReportAction(returnStatus, returnTo);
        }

        report.Status = ContentReportStatus.Dismissed;
        report.ResolvedAtUtc = DateTime.UtcNow;
        report.ResolvedByUserId = AuthorAccess.UserId(User);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("report.dismiss", "ContentReport", id.ToString(), report.Reason, HttpContext);

        await NotifyReporterAsync(
            report,
            title: "گزارش شما بررسی شد",
            body: $"گزارش مربوط به «{report.TargetTitle ?? ("#" + report.TargetId)}» بررسی و رد شد (نیازی به اقدام بیشتر نبود).");

        TempData["FlashOk"] = "گزارش رد شد.";
        return RedirectAfterReportAction(returnStatus, returnTo);
    }

    private IActionResult RedirectAfterReportAction(string? returnStatus, string? returnTo)
    {
        if (string.Equals(returnTo, "moderation", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction("Index", "AdminModeration");
        return RedirectToAction(nameof(Index), new { status = returnStatus ?? "open" });
    }

    private async Task NotifyReporterAsync(ContentReport report, string title, string body)
    {
        if (string.IsNullOrEmpty(report.ReporterUserId)) return;
        try
        {
            await _notify.NotifyAsync(
                report.ReporterUserId,
                NotificationKind.AdminMessage,
                title,
                body,
                "/AdminModeration");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notify reporter after report {Id} failed", report.Id);
        }
    }

    private async Task<bool> CanManageReport(ContentReport report)
    {
        if (AuthorAccess.IsSuperAdmin(User)) return true;
        var uid = AuthorAccess.UserId(User);
        if (uid is null) return false;

        if (report.TargetType == ContentReportTarget.Post)
        {
            var post = await _db.Posts.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == report.TargetId);
            return post is not null && post.AuthorId == uid;
        }

        var comment = await _db.Comments.AsNoTracking()
            .Include(c => c.Post)
            .FirstOrDefaultAsync(c => c.Id == report.TargetId);
        return comment is not null && comment.Post.AuthorId == uid;
    }
}
