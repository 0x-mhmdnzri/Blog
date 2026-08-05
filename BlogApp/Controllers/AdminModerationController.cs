using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using BlogApp.Services.Messaging;
using BlogApp.Services.Performance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

[Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
public class AdminModerationController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notify;
    private readonly IBackgroundJobQueue? _jobs;
    private readonly IUiTranslator _t;
    private readonly ILogger<AdminModerationController> _logger;

    public AdminModerationController(
        ApplicationDbContext db,
        INotificationService notify,
        IUiTranslator t,
        ILogger<AdminModerationController> logger,
        IBackgroundJobQueue? jobs = null)
    {
        _db = db;
        _notify = notify;
        _t = t;
        _logger = logger;
        _jobs = jobs;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = _t["page.moderation"];
        var userId = AuthorAccess.UserId(User)!;
        var seeAll = AuthorAccess.CanModerateAllComments(User);
        var isSuper = AuthorAccess.IsSuperAdmin(User);

        var commentQuery = _db.Comments.AsNoTracking().Include(c => c.Post)
            .Where(c => c.Status == CommentStatus.Pending);
        if (!seeAll)
            commentQuery = commentQuery.Where(c => c.Post.AuthorId == userId);

        var pendingComments = await commentQuery
            .OrderByDescending(c => c.CreatedAtUtc)
            .Take(50)
            .Select(c => new AdminCommentListItem
            {
                Id = c.Id,
                AuthorName = c.AuthorName,
                Body = c.Body,
                CreatedAtUtc = c.CreatedAtUtc,
                Status = c.Status,
                PostId = c.PostId,
                PostTitle = c.Post.Title,
                PostSlug = c.Post.Slug
            })
            .ToListAsync();

        var reportQuery = _db.ContentReports.AsNoTracking()
            .Where(r => r.Status == ContentReportStatus.Open);

        if (!isSuper)
        {
            var myPostIds = await _db.Posts.AsNoTracking().Where(p => p.AuthorId == userId).Select(p => p.Id).ToListAsync();
            var myCommentIds = await _db.Comments.AsNoTracking().Where(c => myPostIds.Contains(c.PostId)).Select(c => c.Id).ToListAsync();
            reportQuery = reportQuery.Where(r =>
                (r.TargetType == ContentReportTarget.Post && myPostIds.Contains(r.TargetId))
                || (r.TargetType == ContentReportTarget.Comment && myCommentIds.Contains(r.TargetId)));
        }

        var openReports = await reportQuery
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(50)
            .ToListAsync();

        List<PendingPostReviewItem> pendingPosts = new();
        if (isSuper)
        {
            pendingPosts = await _db.Posts.AsNoTracking()
                .Where(p => !p.IsDeleted && p.ReviewStatus == PostReviewStatus.PendingReview)
                .OrderByDescending(p => p.UpdatedAtUtc)
                .Take(50)
                .Select(p => new PendingPostReviewItem
                {
                    Id = p.Id,
                    Title = p.Title,
                    Slug = p.Slug,
                    Summary = p.Summary,
                    LanguageCode = p.LanguageCode,
                    AuthorId = p.AuthorId,
                    AuthorName = p.Author != null ? (p.Author.DisplayName ?? p.Author.UserName ?? "") : "",
                    UpdatedAtUtc = p.UpdatedAtUtc,
                    CreatedAtUtc = p.CreatedAtUtc
                })
                .ToListAsync();
        }

        return View(new ModerationQueueViewModel
        {
            PendingComments = pendingComments,
            OpenReports = openReports,
            PendingPosts = pendingPosts
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> ApprovePost(int id)
    {
        // Global EF default is NoTracking — must opt into tracking for mutations.
        var post = await _db.Posts.AsTracking().FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (post is null) return NotFound();
        if (post.ReviewStatus != PostReviewStatus.PendingReview)
        {
            TempData["FlashOk"] = _t["mod.flash_not_pending"];
            return RedirectToAction(nameof(Index));
        }

        post.IsPublished = true;
        post.PublishedAtUtc ??= DateTime.UtcNow;
        post.ScheduledPublishAtUtc = null;
        post.ReviewStatus = PostReviewStatus.Approved;
        post.ReviewNote = null;
        post.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        try { if (_jobs is not null) await _jobs.EnqueueIndexPostAsync(post.Id); }
        catch (Exception ex) { _logger.LogWarning(ex, "Index after ApprovePost {Id}", id); }

        try
        {
            await _notify.NotifyAsync(
                post.AuthorId,
                NotificationKind.NewPost,
                _t["mod.notif_approved_title"],
                _t["mod.notif_approved_body"].Replace("{0}", post.Title),
                "/" + post.LanguageCode + "/post/" + post.Slug);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Notify after ApprovePost {Id}", id); }

        TempData["FlashOk"] = _t["mod.flash_approved"];
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> RejectPost(int id, string? note = null)
    {
        var post = await _db.Posts.AsTracking().FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (post is null) return NotFound();
        if (post.ReviewStatus != PostReviewStatus.PendingReview)
        {
            TempData["FlashOk"] = _t["mod.flash_not_pending"];
            return RedirectToAction(nameof(Index));
        }

        post.IsPublished = false;
        post.ReviewStatus = PostReviewStatus.Rejected;
        if (!string.IsNullOrWhiteSpace(note))
        {
            var n = note.Trim();
            post.ReviewNote = n.Length > 500 ? n[..500] : n;
        }
        else post.ReviewNote = null;
        post.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        try
        {
            var body = string.IsNullOrEmpty(post.ReviewNote)
                ? _t["mod.notif_rejected_body"].Replace("{0}", post.Title)
                : _t["mod.notif_rejected_body_reason"].Replace("{0}", post.Title).Replace("{1}", post.ReviewNote);
            await _notify.NotifyAsync(post.AuthorId, NotificationKind.AdminMessage, _t["mod.notif_rejected_title"], body, "/Posts/Edit/" + post.Id);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Notify after RejectPost {Id}", id); }

        TempData["FlashOk"] = _t["mod.flash_rejected"];
        return RedirectToAction(nameof(Index));
    }
}
