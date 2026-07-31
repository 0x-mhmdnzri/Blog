using BlogApp.Models;
using BlogApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public partial class AdminController
{
    /// <summary>Export ALL posts matching optional search (no page limit).</summary>
    [HttpGet]
    public async Task<IActionResult> PostsExportCsv(string? search = null)
    {
        var userId = AuthorAccess.UserId(User)!;
        var seeAll = AuthorAccess.CanManageAllPosts(User);

        var query = _db.Posts.AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Author)
            .AsQueryable();
        if (!seeAll)
            query = query.Where(p => p.AuthorId == userId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p =>
                p.Title.Contains(term)
                || p.Slug.Contains(term)
                || (p.Category != null && p.Category.Name.Contains(term))
                || p.Author.DisplayName.Contains(term));
        }

        var list = await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new
            {
                p.Id,
                p.Title,
                p.Slug,
                Author = p.Author.DisplayName,
                Category = p.Category != null ? p.Category.Name : "",
                p.IsPublished,
                p.IsFeatured,
                p.IsSticky,
                p.IsDeleted,
                p.ScheduledPublishAtUtc,
                p.CreatedAtUtc,
                p.ViewCount,
                CommentCount = p.Comments.Count
            })
            .ToListAsync();

        var headers = new[]
        {
            "Id", "Title", "Slug", "Author", "Category", "Status",
            "Featured", "Sticky", "Views", "Comments", "CreatedAtUtc"
        };

        var rows = list.Select(p =>
        {
            var status = p.IsDeleted ? "Deleted"
                : p.ScheduledPublishAtUtc.HasValue && !p.IsPublished ? "Scheduled"
                : p.IsPublished ? "Published" : "Draft";
            return new[]
            {
                CsvExport.Cell(p.Id),
                CsvExport.Cell(p.Title),
                CsvExport.Cell(p.Slug),
                CsvExport.Cell(p.Author),
                CsvExport.Cell(p.Category),
                status,
                CsvExport.Cell(p.IsFeatured),
                CsvExport.Cell(p.IsSticky),
                CsvExport.Cell(p.ViewCount),
                CsvExport.Cell(p.CommentCount),
                CsvExport.Cell(p.CreatedAtUtc)
            };
        });

        return CsvExport.File($"posts-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv", headers, rows);
    }

    /// <summary>Export ALL comments for status + optional search (no page limit).</summary>
    [HttpGet]
    public async Task<IActionResult> CommentsExportCsv(string status = "pending", string? search = null)
    {
        var userId = AuthorAccess.UserId(User)!;
        var seeAll = AuthorAccess.CanModerateAllComments(User);

        var query = _db.Comments.AsNoTracking().Include(c => c.Post).AsQueryable();
        if (!seeAll)
            query = query.Where(c => c.Post.AuthorId == userId);

        query = status switch
        {
            "approved" => query.Where(c => c.Status == CommentStatus.Approved),
            "rejected" => query.Where(c => c.Status == CommentStatus.Rejected),
            "all" => query,
            _ => query.Where(c => c.Status == CommentStatus.Pending)
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c =>
                c.AuthorName.Contains(term)
                || c.Body.Contains(term)
                || c.Post.Title.Contains(term));
        }

        var list = await query
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new
            {
                c.Id,
                c.AuthorName,
                c.Body,
                PostTitle = c.Post.Title,
                PostSlug = c.Post.Slug,
                c.Status,
                c.CreatedAtUtc
            })
            .ToListAsync();

        var headers = new[]
        {
            "Id", "AuthorName", "Body", "PostTitle", "PostSlug", "Status", "CreatedAtUtc"
        };

        var rows = list.Select(c => new[]
        {
            CsvExport.Cell(c.Id),
            CsvExport.Cell(c.AuthorName),
            CsvExport.Cell(c.Body),
            CsvExport.Cell(c.PostTitle),
            CsvExport.Cell(c.PostSlug),
            c.Status.ToString(),
            CsvExport.Cell(c.CreatedAtUtc)
        });

        return CsvExport.File($"comments-{status}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv", headers, rows);
    }
}
