using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

/// <summary>Public Finder-style blog browser. Anyone can view published posts; only post owners / SuperAdmin get edit-delete.</summary>
public class BlogController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IUiTranslator _t;

    public BlogController(ApplicationDbContext db, IUiTranslator t)
    {
        _db = db;
        _t = t;
    }

    [AllowAnonymous]
    [HttpGet("/Blog")]
    [HttpGet("/Blogs")]
    public async Task<IActionResult> Index(
        string? scope = null,
        int? folderId = null,
        int? categoryId = null,
        int? tagId = null,
        string? q = null,
        string? sort = null)
    {
        var userId = AuthorAccess.UserId(User);
        var canManageAll = AuthorAccess.CanManageAllPosts(User);
        var isStaff = User.Identity?.IsAuthenticated == true
            && (User.IsInRole(AppRoles.Author) || User.IsInRole(AppRoles.SuperAdmin) || canManageAll);
        var canManage = isStaff;

        // Base set: public sees published only; staff see own (or all if SuperAdmin)
        var postQuery = _db.Posts.AsNoTracking().AsQueryable();
        if (!canManage)
            postQuery = postQuery.Where(p => p.IsPublished && !p.IsDeleted);
        else if (!canManageAll && userId != null)
            postQuery = postQuery.Where(p => p.AuthorId == userId || (p.IsPublished && !p.IsDeleted));

        var publishedCount = await postQuery.CountAsync(p => p.IsPublished && !p.IsDeleted);
        var draftCount = canManage
            ? await postQuery.CountAsync(p => !p.IsPublished && !p.IsDeleted && p.ReviewStatus != PostReviewStatus.PendingReview)
            : 0;
        var pendingCount = canManage
            ? await postQuery.CountAsync(p => !p.IsDeleted && !p.IsPublished && p.ReviewStatus == PostReviewStatus.PendingReview)
            : 0;
        var trashCount = canManage
            ? await postQuery.CountAsync(p => p.IsDeleted)
            : 0;
        var allCount = canManage
            ? await postQuery.CountAsync(p => !p.IsDeleted)
            : publishedCount;

        scope = (scope ?? "all").Trim().ToLowerInvariant();
        if (!canManage)
            scope = "published"; // visitors locked to published

        var list = postQuery;
        list = scope switch
        {
            "published" => list.Where(p => p.IsPublished && !p.IsDeleted),
            "draft" when canManage => list.Where(p => !p.IsPublished && !p.IsDeleted && p.ReviewStatus != PostReviewStatus.PendingReview),
            "pending" when canManage => list.Where(p => !p.IsDeleted && !p.IsPublished && p.ReviewStatus == PostReviewStatus.PendingReview),
            "trash" when canManage => list.Where(p => p.IsDeleted),
            _ => list.Where(p => !p.IsDeleted && (canManage || p.IsPublished))
        };

        if (folderId is int fid && fid > 0)
            list = list.Where(p => _db.PostFolderItems.Any(i => i.FolderId == fid && i.PostId == p.Id));
        if (categoryId is int cid && cid > 0)
            list = list.Where(p => p.CategoryId == cid);
        if (tagId is int tid && tid > 0)
            list = list.Where(p => p.PostTags.Any(pt => pt.TagId == tid));
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            list = list.Where(p => p.Title.Contains(term) || p.Slug.Contains(term)
                || (p.Summary != null && p.Summary.Contains(term)));
        }

        list = (sort ?? "recent") switch
        {
            "title" => list.OrderBy(p => p.Title),
            "oldest" => list.OrderBy(p => p.UpdatedAtUtc),
            "views" => list.OrderByDescending(p => p.ViewCount),
            _ => list.OrderByDescending(p => p.UpdatedAtUtc)
        };

        var posts = await list.Take(400)
            .Select(p => new FinderPostItem
            {
                Id = p.Id,
                Title = p.Title,
                Slug = p.Slug,
                AuthorId = p.AuthorId,
                CategoryName = p.Category != null ? p.Category.Name : null,
                CategoryId = p.CategoryId,
                AuthorName = p.Author.DisplayName,
                IsPublished = p.IsPublished,
                IsDeleted = p.IsDeleted,
                IsFeatured = p.IsFeatured,
                ReviewStatus = p.ReviewStatus,
                ScheduledPublishAtUtc = p.ScheduledPublishAtUtc,
                UpdatedAtUtc = p.UpdatedAtUtc,
                ViewCount = p.ViewCount,
                CoverUrl = p.CoverMediaAssetId != null ? "/media/" + p.CoverMediaAssetId : null,
                TagNames = p.PostTags.Select(pt => pt.Tag.Name).ToList()
            }).ToListAsync();

        var folderQuery = _db.PostFolders.AsNoTracking().AsQueryable();
        if (canManage && !canManageAll && userId != null)
            folderQuery = folderQuery.Where(f => f.OwnerUserId == userId);
        else if (!canManage)
            folderQuery = folderQuery.Where(_ => false); // hide private folders from public

        var folders = canManage
            ? await folderQuery
                .OrderBy(f => f.DisplayOrder).ThenBy(f => f.Name)
                .Select(f => new FinderFolderItem
                {
                    Id = f.Id,
                    Name = f.Name,
                    Color = f.Color,
                    PostCount = f.Items.Count
                }).ToListAsync()
            : new List<FinderFolderItem>();

        var categories = await _db.Categories.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new FinderSideItem
            {
                Id = c.Id,
                Name = c.Name,
                Count = c.Posts.Count(p => p.IsPublished && !p.IsDeleted)
            }).ToListAsync();

        var tags = await _db.Tags.AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new FinderSideItem
            {
                Id = t.Id,
                Name = t.Name,
                Count = t.PostTags.Count(pt => pt.Post.IsPublished && !pt.Post.IsDeleted)
            })
            .Where(t => t.Count > 0)
            .Take(40)
            .ToListAsync();

        var vm = new PostsFinderViewModel
        {
            Scope = scope,
            FolderId = folderId,
            CategoryId = categoryId,
            TagId = tagId,
            Search = q,
            Sort = sort ?? "recent",
            ShowAuthor = canManageAll,
            CanManage = canManage,
            CanManageAll = canManageAll,
            CurrentUserId = userId,
            IsPublicSurface = true,
            AllCount = allCount,
            PublishedCount = publishedCount,
            DraftCount = draftCount,
            PendingCount = pendingCount,
            TrashCount = trashCount,
            Posts = posts,
            Folders = folders,
            Categories = categories,
            Tags = tags
        };

        return View("~/Views/Admin/Posts.cshtml", vm);
    }
}
