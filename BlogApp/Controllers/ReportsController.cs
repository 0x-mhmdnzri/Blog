using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

/// <summary>Public content report submission (rate-limited).</summary>
[AllowAnonymous]
[EnableRateLimiting("comment")]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ISiteConfigService _config;

    public ReportsController(ApplicationDbContext db, ISiteConfigService config)
    {
        _db = db;
        _config = config;
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
    public async Task<IActionResult> Submit(ReportDto dto, string? returnUrl = null)
    {
        if (!await _config.IsFlagEnabledAsync(FeatureFlagKeys.PublicReports))
        {
            TempData["ReportMsg"] = "گزارش محتوا فعلاً غیرفعال است.";
            return LocalRedirect(returnUrl ?? "/");
        }

        dto.Reason = (dto.Reason ?? "").Trim();
        dto.Details = (dto.Details ?? "").Trim();
        dto.ReporterName = (dto.ReporterName ?? "").Trim();

        if (dto.TargetId <= 0 || dto.Reason.Length is < 2 or > 80)
        {
            TempData["ReportMsg"] = "دلیل گزارش معتبر نیست.";
            return LocalRedirect(returnUrl ?? "/");
        }

        if (dto.Details.Length > 1000) dto.Details = dto.Details[..1000];

        string? title = null;
        ContentReportTarget target;

        if (string.Equals(dto.TargetType, "comment", StringComparison.OrdinalIgnoreCase))
        {
            target = ContentReportTarget.Comment;
            var c = await _db.Comments.Include(x => x.Post).FirstOrDefaultAsync(x => x.Id == dto.TargetId);
            if (c is null)
            {
                TempData["ReportMsg"] = "دیدگاه یافت نشد.";
                return LocalRedirect(returnUrl ?? "/");
            }
            title = $"دیدگاه روی «{c.Post.Title}»";
        }
        else
        {
            target = ContentReportTarget.Post;
            var p = await _db.Posts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == dto.TargetId && !x.IsDeleted);
            if (p is null)
            {
                TempData["ReportMsg"] = "نوشته یافت نشد.";
                return LocalRedirect(returnUrl ?? "/");
            }
            title = p.Title;
        }

        var reporterId = AuthorAccess.UserId(User);
        var reporterName = !string.IsNullOrEmpty(dto.ReporterName)
            ? dto.ReporterName
            : User.Identity?.Name;

        _db.ContentReports.Add(new ContentReport
        {
            TargetType = target,
            TargetId = dto.TargetId,
            TargetTitle = title,
            Reason = dto.Reason,
            Details = string.IsNullOrWhiteSpace(dto.Details) ? null : dto.Details,
            ReporterUserId = reporterId,
            ReporterName = reporterName,
            Status = ContentReportStatus.Open,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        TempData["ReportMsg"] = "گزارش شما ثبت شد و بررسی خواهد شد.";
        return LocalRedirect(returnUrl ?? "/");
    }
}
