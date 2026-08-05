using System.Security.Claims;
using BlogApp.Data;
using BlogApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Services;

/// <summary>Pending / new counts for admin sidebar badges (not personal inbox unread).</summary>
public interface IAdminNavBadgeService
{
    Task<IReadOnlyDictionary<string, int>> GetCountsAsync(ClaimsPrincipal user, CancellationToken ct = default);
}

public sealed class AdminNavBadgeService : IAdminNavBadgeService
{
    private readonly ApplicationDbContext _db;

    public AdminNavBadgeService(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyDictionary<string, int>> GetCountsAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var userId = AuthorAccess.UserId(user);
        var isSuper = AuthorAccess.IsSuperAdmin(user);
        var seeAllComments = AuthorAccess.CanModerateAllComments(user);

        var commentQ = _db.Comments.AsNoTracking().Where(c => c.Status == CommentStatus.Pending);
        if (!seeAllComments && userId is not null)
            commentQ = commentQ.Where(c => c.Post.AuthorId == userId);
        var pendingComments = await commentQ.CountAsync(ct);
        if (pendingComments > 0)
            result["comments"] = pendingComments;

        var reportQ = _db.ContentReports.AsNoTracking().Where(r => r.Status == ContentReportStatus.Open);
        if (!isSuper && userId is not null)
        {
            var myPostIds = await _db.Posts.AsNoTracking()
                .Where(p => p.AuthorId == userId)
                .Select(p => p.Id)
                .ToListAsync(ct);
            var myCommentIds = await _db.Comments.AsNoTracking()
                .Where(c => myPostIds.Contains(c.PostId))
                .Select(c => c.Id)
                .ToListAsync(ct);
            reportQ = reportQ.Where(r =>
                (r.TargetType == ContentReportTarget.Post && myPostIds.Contains(r.TargetId))
                || (r.TargetType == ContentReportTarget.Comment && myCommentIds.Contains(r.TargetId)));
        }
        var openReports = await reportQ.CountAsync(ct);
        if (openReports > 0)
            result["reports"] = openReports;

        var pendingPosts = 0;
        if (isSuper)
        {
            pendingPosts = await _db.Posts.AsNoTracking()
                .CountAsync(p => !p.IsDeleted && p.ReviewStatus == PostReviewStatus.PendingReview, ct);
            if (pendingPosts > 0)
                result["posts"] = pendingPosts;
        }

        var modTotal = pendingComments + openReports + pendingPosts;
        if (modTotal > 0)
            result["moderation"] = modTotal;

        if (isSuper)
        {
            var apiPending = await _db.ApiKeys.AsNoTracking()
                .CountAsync(k => k.ApprovalStatus == ApiKeyApprovalStatus.Pending && !k.IsBanned, ct);
            if (apiPending > 0)
                result["apikeys"] = apiPending;

            var donations = await _db.Donations.AsNoTracking()
                .CountAsync(d => d.Status == DonationStatus.Pending, ct);
            if (donations > 0)
                result["monetization"] = donations;
        }

        // Unread personal notifications: navbar bell only.
        // AdminNotifications sidebar is for broadcast / admin campaigns — no badge.

        return result;
    }
}
