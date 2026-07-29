using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

/// <summary>Unified moderation queue: pending comments + open content reports.</summary>
[Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
public class AdminModerationController : Controller
{
    private readonly ApplicationDbContext _db;

    public AdminModerationController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "صف بررسی";
        var userId = AuthorAccess.UserId(User)!;
        var seeAll = AuthorAccess.CanModerateAllComments(User);

        var commentQuery = _db.Comments.Include(c => c.Post)
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

        if (!AuthorAccess.IsSuperAdmin(User))
        {
            var myPostIds = await _db.Posts.Where(p => p.AuthorId == userId).Select(p => p.Id).ToListAsync();
            var myCommentIds = await _db.Comments.Where(c => myPostIds.Contains(c.PostId)).Select(c => c.Id).ToListAsync();
            reportQuery = reportQuery.Where(r =>
                (r.TargetType == ContentReportTarget.Post && myPostIds.Contains(r.TargetId))
                || (r.TargetType == ContentReportTarget.Comment && myCommentIds.Contains(r.TargetId)));
        }

        var openReports = await reportQuery.OrderByDescending(r => r.CreatedAtUtc).Take(50).ToListAsync();

        return View(new ModerationQueueViewModel
        {
            PendingComments = pendingComments,
            OpenReports = openReports
        });
    }
}
