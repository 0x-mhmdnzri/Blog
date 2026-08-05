using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services;
using BlogApp.Services.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

/// <summary>
/// Authenticated content report submission (rate-limited).
/// Reports are queued as Open (pending) for SuperAdmin and post owner review.
/// Submitting a report never deletes or unpublishes the target post/comment.
/// </summary>
[Authorize]
[EnableRateLimiting("comment")]
public class ReportsController : Controller
{
    private static readonly Dictionary<string, string> ReasonLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["spam"] = "هرزنامه / تبلیغات",
        ["harassment"] = "توهین یا آزار",
        ["misinfo"] = "اطلاعات نادرست",
        ["copyright"] = "نقض حق نشر",
        ["other"] = "سایر",
    };

    private readonly ApplicationDbContext _db;
    private readonly ISiteConfigService _config;
    private readonly INotificationService _notify;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ILogger<ReportsController> _log;

    public ReportsController(
        ApplicationDbContext db,
        ISiteConfigService config,
        INotificationService notify,
        UserManager<ApplicationUser> users,
        ILogger<ReportsController> log)
    {
        _db = db;
        _config = config;
        _notify = notify;
        _users = users;
        _log = log;
    }

    public class ReportDto
    {
        public string TargetType { get; set; } = "post";
        public int TargetId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? Details { get; set; }
        public string? ReporterName { get; set; }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(ReportDto dto, string? returnUrl = null, CancellationToken ct = default)
    {
        if (!await _config.IsFlagEnabledAsync(FeatureFlagKeys.PublicReports))
        {
            TempData["ReportMsg"] = "گزارش محتوا فعلاً غیرفعال است.";
            return LocalRedirect(SafeReturn(returnUrl));
        }

        dto.Reason = (dto.Reason ?? "").Trim();
        dto.Details = (dto.Details ?? "").Trim();
        dto.ReporterName = (dto.ReporterName ?? "").Trim();

        if (dto.TargetId <= 0 || dto.Reason.Length is < 2 or > 80)
        {
            TempData["ReportMsg"] = "دلیل گزارش معتبر نیست.";
            return LocalRedirect(SafeReturn(returnUrl));
        }

        if (dto.Details.Length < 8)
        {
            TempData["ReportMsg"] = "توضیح گزارش الزامی است (حداقل ۸ نویسه).";
            return LocalRedirect(SafeReturn(returnUrl));
        }

        if (dto.Details.Length > 1000) dto.Details = dto.Details[..1000];

        var reasonCode = dto.Reason.ToLowerInvariant();
        var reasonDisplay = ReasonLabels.TryGetValue(reasonCode, out var label)
            ? label
            : dto.Reason;

        string? title = null;
        string? ownerUserId = null;
        string? postSlug = null;
        string? postLang = null;
        ContentReportTarget target;

        if (string.Equals(dto.TargetType, "comment", StringComparison.OrdinalIgnoreCase))
        {
            target = ContentReportTarget.Comment;
            var c = await _db.Comments.Include(x => x.Post)
                .FirstOrDefaultAsync(x => x.Id == dto.TargetId, ct);
            if (c is null)
            {
                TempData["ReportMsg"] = "دیدگاه یافت نشد.";
                return LocalRedirect(SafeReturn(returnUrl));
            }
            title = $"دیدگاه روی «{c.Post.Title}»";
            ownerUserId = c.Post.AuthorId;
            postSlug = c.Post.Slug;
            postLang = c.Post.LanguageCode;
        }
        else
        {
            target = ContentReportTarget.Post;
            var p = await _db.Posts.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == dto.TargetId && !x.IsDeleted, ct);
            if (p is null)
            {
                TempData["ReportMsg"] = "نوشته یافت نشد.";
                return LocalRedirect(SafeReturn(returnUrl));
            }
            title = p.Title;
            ownerUserId = p.AuthorId;
            postSlug = p.Slug;
            postLang = p.LanguageCode;
        }

        var reporterId = AuthorAccess.UserId(User);
        var reporterName = !string.IsNullOrEmpty(dto.ReporterName)
            ? dto.ReporterName
            : (User.Identity?.Name ?? "کاربر");

        if (!string.IsNullOrEmpty(reporterId))
        {
            var since = DateTime.UtcNow.AddHours(-1);
            var dup = await _db.ContentReports.AsNoTracking().AnyAsync(r =>
                r.ReporterUserId == reporterId
                && r.TargetType == target
                && r.TargetId == dto.TargetId
                && r.Status == ContentReportStatus.Open
                && r.CreatedAtUtc >= since, ct);
            if (dup)
            {
                TempData["ReportOk"] = "1";
                TempData["ReportMsg"] = "گزارش شما قبلاً ثبت شده و در صف بررسی (در انتظار تأیید) است.";
                return LocalRedirect(SafeReturn(returnUrl, postLang, postSlug));
            }
        }

        _db.ContentReports.Add(new ContentReport
        {
            TargetType = target,
            TargetId = dto.TargetId,
            TargetTitle = title,
            Reason = reasonDisplay,
            Details = string.IsNullOrWhiteSpace(dto.Details) ? null : dto.Details,
            ReporterUserId = reporterId,
            ReporterName = reporterName,
            Status = ContentReportStatus.Open,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        await NotifyStakeholdersAsync(
            ownerUserId,
            reporterId,
            reporterName,
            title ?? "",
            reasonDisplay,
            dto.Details,
            postLang,
            postSlug,
            ct);

        TempData["ReportOk"] = "1";
        TempData["ReportMsg"] =
            "گزارش شما ثبت شد و در وضعیت «در انتظار بررسی» قرار گرفت. نویسنده و مدیر آن را بررسی می‌کنند؛ نوشته حذف نمی‌شود.";
        return LocalRedirect(SafeReturn(returnUrl, postLang, postSlug));
    }

    private async Task NotifyStakeholdersAsync(
        string? ownerUserId,
        string? reporterId,
        string reporterName,
        string title,
        string reason,
        string? details,
        string? postLang,
        string? postSlug,
        CancellationToken ct)
    {
        try
        {
            var link = !string.IsNullOrEmpty(postLang) && !string.IsNullOrEmpty(postSlug)
                ? $"/{postLang}/post/{postSlug}"
                : "/AdminModeration";
            var adminLink = "/AdminModeration";

            var bodyOwner =
                $"نوشته «{title}» گزارش شد (در انتظار بررسی).\nدلیل: {reason}"
                + (string.IsNullOrWhiteSpace(details) ? "" : $"\nتوضیح: {details.Trim()}");

            var bodyAdmin =
                $"گزارش جدید از «{reporterName}» روی «{title}» (در انتظار بررسی).\nدلیل: {reason}"
                + (string.IsNullOrWhiteSpace(details) ? "" : $"\nتوضیح: {details.Trim()}");

            if (!string.IsNullOrEmpty(ownerUserId)
                && !string.Equals(ownerUserId, reporterId, StringComparison.Ordinal))
            {
                await _notify.NotifyAsync(
                    ownerUserId,
                    NotificationKind.AdminMessage,
                    "گزارش روی نوشته شما — در انتظار بررسی",
                    bodyOwner,
                    link,
                    ct);
            }

            var supers = await _users.GetUsersInRoleAsync(AppRoles.SuperAdmin);
            foreach (var s in supers)
            {
                if (string.IsNullOrEmpty(s.Id)) continue;
                if (string.Equals(s.Id, reporterId, StringComparison.Ordinal)) continue;
                if (string.Equals(s.Id, ownerUserId, StringComparison.Ordinal)) continue;

                await _notify.NotifyAsync(
                    s.Id,
                    NotificationKind.AdminMessage,
                    "گزارش محتوای جدید — در انتظار بررسی",
                    bodyAdmin,
                    adminLink,
                    ct);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Report notification failed Title={Title}", title);
        }
    }

    private static string SafeReturn(string? returnUrl, string? lang = null, string? slug = null)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl)
            && returnUrl.StartsWith('/')
            && !returnUrl.StartsWith("//", StringComparison.Ordinal))
            return returnUrl;

        if (!string.IsNullOrEmpty(lang) && !string.IsNullOrEmpty(slug))
            return $"/{lang}/post/{slug}";

        return "/";
    }
}
