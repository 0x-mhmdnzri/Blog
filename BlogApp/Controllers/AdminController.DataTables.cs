using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public partial class AdminController
{
    /// <summary>Conventional route: /Admin/Posts. Finder UI is page-local (Layout=null).</summary>
    public async Task<IActionResult> Posts(string? scope = null, int? folderId = null, int? categoryId = null, int? tagId = null, string? q = null, string? sort = null)
    {
        ViewBag.ShowAuthorColumn = AuthorAccess.CanManageAllPosts(User);
        var userId = AuthorAccess.UserId(User)!;
        var seeAll = AuthorAccess.CanManageAllPosts(User);

        var postQuery = _db.Posts.AsNoTracking().AsQueryable();
        if (!seeAll)
            postQuery = postQuery.Where(p => p.AuthorId == userId);

        var allCount = await postQuery.CountAsync();
        var publishedCount = await postQuery.CountAsync(p => p.IsPublished && !p.IsDeleted);
        var draftCount = await postQuery.CountAsync(p => !p.IsPublished && !p.IsDeleted && p.ReviewStatus != PostReviewStatus.PendingReview);
        var pendingCount = await postQuery.CountAsync(p => !p.IsDeleted && !p.IsPublished && p.ReviewStatus == PostReviewStatus.PendingReview);
        var trashCount = await postQuery.CountAsync(p => p.IsDeleted);

        scope = (scope ?? "all").Trim().ToLowerInvariant();
        var list = postQuery;
        list = scope switch
        {
            "published" => list.Where(p => p.IsPublished && !p.IsDeleted),
            "draft" => list.Where(p => !p.IsPublished && !p.IsDeleted && p.ReviewStatus != PostReviewStatus.PendingReview),
            "pending" => list.Where(p => !p.IsDeleted && !p.IsPublished && p.ReviewStatus == PostReviewStatus.PendingReview),
            "trash" => list.Where(p => p.IsDeleted),
            _ => list.Where(p => !p.IsDeleted)
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
        if (!seeAll)
            folderQuery = folderQuery.Where(f => f.OwnerUserId == userId);

        var folders = await folderQuery
            .OrderBy(f => f.DisplayOrder).ThenBy(f => f.Name)
            .Select(f => new FinderFolderItem
            {
                Id = f.Id,
                Name = f.Name,
                Color = f.Color,
                PostCount = f.Items.Count
            }).ToListAsync();

        var categories = await _db.Categories.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new FinderSideItem { Id = c.Id, Name = c.Name, Count = c.Posts.Count(p => !p.IsDeleted) })
            .ToListAsync();

        var tags = await _db.Tags.AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new FinderSideItem { Id = t.Id, Name = t.Name, Count = t.PostTags.Count })
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
            ShowAuthor = seeAll,
            CanManage = true,
            CanManageAll = seeAll,
            CurrentUserId = userId,
            IsPublicSurface = false,
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
        return View("Posts", vm);
    }

    [HttpGet]
    public async Task<IActionResult> PostsData()
    {
        var req = DataTablesRequest.From(Request);
        var userId = AuthorAccess.UserId(User)!;
        var seeAll = AuthorAccess.CanManageAllPosts(User);

        var query = _db.Posts.AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Author)
            .AsQueryable();
        if (!seeAll)
            query = query.Where(p => p.AuthorId == userId);

        var total = await query.CountAsync();

        if (!string.IsNullOrWhiteSpace(req.SearchValue))
        {
            var term = req.SearchValue;
            query = query.Where(p =>
                p.Title.Contains(term)
                || p.Slug.Contains(term)
                || (p.Category != null && p.Category.Name.Contains(term))
                || p.Author.DisplayName.Contains(term));
        }

        query = ApplyPostsColumnFilters(query, req, seeAll);

        var filtered = await query.CountAsync();
        query = ApplyPostsOrder(query, req, seeAll);

        var page = await query
            .Skip(req.Start)
            .Take(req.Length)
            .Select(p => new
            {
                p.Id,
                p.Title,
                p.Slug,
                CategoryName = p.Category != null ? p.Category.Name : (string?)null,
                p.IsPublished,
                p.IsFeatured,
                p.IsSticky,
                p.IsDeleted,
                p.ScheduledPublishAtUtc,
                p.ReviewStatus,
                p.CreatedAtUtc,
                p.ViewCount,
                CommentCount = p.Comments.Count,
                AuthorDisplayName = p.Author.DisplayName
            })
            .ToListAsync();

        var token = GetAntiforgeryToken();
        var dash = _t["msg.dash"];
        var rows = page.Select((p, i) =>
        {
            var idx = req.Start + i + 1;
            var statusHtml = StatusPill(p.IsDeleted, p.IsPublished, p.ScheduledPublishAtUtc, p.ReviewStatus);
            var featuresHtml = FeaturesHtml(p.IsFeatured, p.IsSticky);
            var actionsHtml = PostActionsHtml(p.Id, p.Title, p.IsDeleted, token);
            var titleHtml = $"<a href=\"/post/{System.Net.WebUtility.HtmlEncode(p.Slug)}\" dir=\"auto\">{System.Net.WebUtility.HtmlEncode(p.Title)}</a>";

            if (seeAll)
            {
                return new object[]
                {
                    idx,
                    titleHtml,
                    System.Net.WebUtility.HtmlEncode(p.AuthorDisplayName),
                    System.Net.WebUtility.HtmlEncode(p.CategoryName ?? dash),
                    statusHtml,
                    featuresHtml,
                    p.ViewCount,
                    p.CommentCount,
                    PersianDate.Date(p.CreatedAtUtc),
                    actionsHtml
                };
            }

            return new object[]
            {
                idx,
                titleHtml,
                System.Net.WebUtility.HtmlEncode(p.CategoryName ?? dash),
                statusHtml,
                featuresHtml,
                p.ViewCount,
                p.CommentCount,
                PersianDate.Date(p.CreatedAtUtc),
                actionsHtml
            };
        }).ToList();

        return Json(DataTablesResponse.Ok(req.Draw, total, filtered, rows));
    }

    private static IQueryable<Post> ApplyPostsColumnFilters(IQueryable<Post> query, DataTablesRequest req, bool seeAll)
    {
        if (req.Col(1) is { } title)
            query = query.Where(p => p.Title.Contains(title) || p.Slug.Contains(title));

        if (seeAll)
        {
            if (req.Col(2) is { } author)
                query = query.Where(p => p.Author.DisplayName.Contains(author) || (p.Author.UserName != null && p.Author.UserName.Contains(author)));
            if (req.Col(3) is { } cat)
                query = query.Where(p => p.Category != null && p.Category.Name.Contains(cat));
            if (req.Col(4) is { } status)
                query = ApplyPostStatusFilter(query, status);
        }
        else
        {
            if (req.Col(2) is { } cat2)
                query = query.Where(p => p.Category != null && p.Category.Name.Contains(cat2));
            if (req.Col(3) is { } status2)
                query = ApplyPostStatusFilter(query, status2);
        }

        return query;
    }

    private static IQueryable<Post> ApplyPostStatusFilter(IQueryable<Post> query, string status)
    {
        return status.ToLowerInvariant() switch
        {
            "published" => query.Where(p => p.IsPublished && !p.IsDeleted),
            "pending" or "pending_review" => query.Where(p => !p.IsDeleted && !p.IsPublished && p.ReviewStatus == PostReviewStatus.PendingReview),
            "draft" => query.Where(p => !p.IsPublished && !p.IsDeleted && p.ScheduledPublishAtUtc == null && p.ReviewStatus != PostReviewStatus.PendingReview),
            "scheduled" => query.Where(p => !p.IsPublished && !p.IsDeleted && p.ScheduledPublishAtUtc != null),
            "deleted" => query.Where(p => p.IsDeleted),
            _ => query
        };
    }

    private static IQueryable<Post> ApplyPostsOrder(IQueryable<Post> query, DataTablesRequest req, bool seeAll)
    {
        if (seeAll)
        {
            return (req.OrderColumn, req.Asc) switch
            {
                (1, true) => query.OrderBy(p => p.Title),
                (1, false) => query.OrderByDescending(p => p.Title),
                (2, true) => query.OrderBy(p => p.Author.DisplayName),
                (2, false) => query.OrderByDescending(p => p.Author.DisplayName),
                (3, true) => query.OrderBy(p => p.Category != null ? p.Category.Name : ""),
                (3, false) => query.OrderByDescending(p => p.Category != null ? p.Category.Name : ""),
                (4, true) => query.OrderBy(p => p.IsDeleted).ThenBy(p => p.IsPublished),
                (4, false) => query.OrderByDescending(p => p.IsDeleted).ThenByDescending(p => p.IsPublished),
                (6, true) => query.OrderBy(p => p.ViewCount),
                (6, false) => query.OrderByDescending(p => p.ViewCount),
                (7, true) => query.OrderBy(p => p.Comments.Count),
                (7, false) => query.OrderByDescending(p => p.Comments.Count),
                (8, true) => query.OrderBy(p => p.CreatedAtUtc),
                (8, false) => query.OrderByDescending(p => p.CreatedAtUtc),
                _ => query.OrderByDescending(p => p.CreatedAtUtc)
            };
        }

        return (req.OrderColumn, req.Asc) switch
        {
            (1, true) => query.OrderBy(p => p.Title),
            (1, false) => query.OrderByDescending(p => p.Title),
            (2, true) => query.OrderBy(p => p.Category != null ? p.Category.Name : ""),
            (2, false) => query.OrderByDescending(p => p.Category != null ? p.Category.Name : ""),
            (3, true) => query.OrderBy(p => p.IsDeleted).ThenBy(p => p.IsPublished),
            (3, false) => query.OrderByDescending(p => p.IsDeleted).ThenByDescending(p => p.IsPublished),
            (5, true) => query.OrderBy(p => p.ViewCount),
            (5, false) => query.OrderByDescending(p => p.ViewCount),
            (6, true) => query.OrderBy(p => p.Comments.Count),
            (6, false) => query.OrderByDescending(p => p.Comments.Count),
            (7, true) => query.OrderBy(p => p.CreatedAtUtc),
            (7, false) => query.OrderByDescending(p => p.CreatedAtUtc),
            _ => query.OrderByDescending(p => p.CreatedAtUtc)
        };
    }

    private string StatusPill(bool deleted, bool published, DateTime? scheduled, PostReviewStatus review = PostReviewStatus.None)
    {
        if (deleted) return $"<span class=\"status-pill rejected\">{System.Net.WebUtility.HtmlEncode(_t["status.deleted"])}</span>";
        if (review == PostReviewStatus.PendingReview && !published)
            return $"<span class=\"status-pill pending\">{System.Net.WebUtility.HtmlEncode(_t["post.status_pending_review"])}</span>";
        if (review == PostReviewStatus.Rejected && !published)
            return $"<span class=\"status-pill rejected\">{System.Net.WebUtility.HtmlEncode(_t["status.rejected"])}</span>";
        if (scheduled.HasValue && !published) return $"<span class=\"status-pill scheduled\">{System.Net.WebUtility.HtmlEncode(_t["status.scheduled"])}</span>";
        if (published) return $"<span class=\"status-pill published\">{System.Net.WebUtility.HtmlEncode(_t["status.published_full"])}</span>";
        return $"<span class=\"status-pill draft\">{System.Net.WebUtility.HtmlEncode(_t["status.draft"])}</span>";
    }

    private string FeaturesHtml(bool featured, bool sticky)
    {
        if (!featured && !sticky) return $"<span class=\"text-muted-dark small\">{System.Net.WebUtility.HtmlEncode(_t["msg.dash"])}</span>";
        var parts = new List<string>();
        if (featured) parts.Add($"<span class=\"status-pill featured\">{System.Net.WebUtility.HtmlEncode(_t["status.featured"])}</span>");
        if (sticky) parts.Add($"<span class=\"status-pill sticky\">{System.Net.WebUtility.HtmlEncode(_t["status.sticky"])}</span>");
        return $"<div class=\"d-flex gap-1 flex-wrap\">{string.Join("", parts)}</div>";
    }

    private string PostActionsHtml(int id, string title, bool deleted, string token)
    {
        if (deleted)
        {
            return $"<form method=\"post\" action=\"/Posts/Restore\" class=\"d-inline\">" +
                   $"<input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"{token}\" />" +
                   $"<input type=\"hidden\" name=\"id\" value=\"{id}\" />" +
                   $"<button type=\"submit\" class=\"icon-btn approve\">{System.Net.WebUtility.HtmlEncode(_t["btn.restore"])}</button></form>";
        }

        var attrTitle = System.Net.WebUtility.HtmlEncode(title ?? "").Replace("\"", "");
        return $"<div class=\"d-flex gap-1 flex-wrap\">" +
               $"<a class=\"icon-btn\" href=\"/Posts/Edit/{id}\">{System.Net.WebUtility.HtmlEncode(_t["btn.edit"])}</a>" +
               $"<form method=\"post\" action=\"/Posts/Duplicate\" class=\"d-inline\">" +
               $"<input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"{token}\" />" +
               $"<input type=\"hidden\" name=\"id\" value=\"{id}\" />" +
               $"<button type=\"submit\" class=\"icon-btn\">{System.Net.WebUtility.HtmlEncode(_t["btn.duplicate"])}</button></form>" +
               $"<button type=\"button\" class=\"icon-btn reject\" data-post-delete " +
               $"data-id=\"{id}\" data-title=\"{attrTitle}\" title=\"{System.Net.WebUtility.HtmlEncode(_t["btn.delete"])}\">" +
               $"{System.Net.WebUtility.HtmlEncode(_t["btn.delete"])}</button></div>";
    }

    private string GetAntiforgeryToken()
    {
        var af = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>();
        return af.GetAndStoreTokens(HttpContext).RequestToken ?? "";
    }
}
