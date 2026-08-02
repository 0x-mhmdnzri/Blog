using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public partial class AdminController
{
    /// <summary>Empty shell — rows loaded via PostsData.</summary>
    public IActionResult Posts()
    {
        ViewBag.ShowAuthorColumn = AuthorAccess.CanManageAllPosts(User);
        return View();
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
            var statusHtml = StatusPill(p.IsDeleted, p.IsPublished, p.ScheduledPublishAtUtc);
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
            "draft" => query.Where(p => !p.IsPublished && !p.IsDeleted && p.ScheduledPublishAtUtc == null),
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

    private string StatusPill(bool deleted, bool published, DateTime? scheduled)
    {
        if (deleted) return $"<span class=\"status-pill rejected\">{System.Net.WebUtility.HtmlEncode(_t["status.deleted"])}</span>";
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

    public async Task<IActionResult> Comments(string status = "pending")
    {
        var userId = AuthorAccess.UserId(User)!;
        var seeAll = AuthorAccess.CanModerateAllComments(User);
        var baseComments = _db.Comments.AsQueryable();
        if (!seeAll)
            baseComments = baseComments.Where(c => c.Post.AuthorId == userId);

        ViewBag.CurrentStatus = status;
        ViewBag.PendingCount = await baseComments.CountAsync(c => c.Status == CommentStatus.Pending);
        ViewBag.ApprovedCount = await baseComments.CountAsync(c => c.Status == CommentStatus.Approved);
        ViewBag.RejectedCount = await baseComments.CountAsync(c => c.Status == CommentStatus.Rejected);
        ViewBag.SpamCount = await baseComments.CountAsync(c => c.Status == CommentStatus.Spam);
        ViewBag.AllCount = await baseComments.CountAsync();
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> CommentsData(string status = "pending")
    {
        var req = DataTablesRequest.From(Request);
        var userId = AuthorAccess.UserId(User)!;
        var seeAll = AuthorAccess.CanModerateAllComments(User);

        var query = _db.Comments.AsNoTracking().Include(c => c.Post).AsQueryable();
        if (!seeAll)
            query = query.Where(c => c.Post.AuthorId == userId);

        query = status switch
        {
            "approved" => query.Where(c => c.Status == CommentStatus.Approved),
            "rejected" => query.Where(c => c.Status == CommentStatus.Rejected),
            "spam" => query.Where(c => c.Status == CommentStatus.Spam),
            "all" => query,
            _ => query.Where(c => c.Status == CommentStatus.Pending)
        };

        var total = await query.CountAsync();

        if (!string.IsNullOrWhiteSpace(req.SearchValue))
        {
            var term = req.SearchValue;
            query = query.Where(c =>
                c.AuthorName.Contains(term)
                || c.Body.Contains(term)
                || c.Post.Title.Contains(term));
        }

        if (req.Col(1) is { } author)
            query = query.Where(c => c.AuthorName.Contains(author));
        if (req.Col(2) is { } body)
            query = query.Where(c => c.Body.Contains(body));
        if (req.Col(3) is { } post)
            query = query.Where(c => c.Post.Title.Contains(post));
        if (req.Col(5) is { } st)
        {
            query = st.ToLowerInvariant() switch
            {
                "pending" => query.Where(c => c.Status == CommentStatus.Pending),
                "approved" => query.Where(c => c.Status == CommentStatus.Approved),
                "rejected" => query.Where(c => c.Status == CommentStatus.Rejected),
                "spam" => query.Where(c => c.Status == CommentStatus.Spam),
                _ => query
            };
        }

        var filtered = await query.CountAsync();

        query = (req.OrderColumn, req.Asc) switch
        {
            (1, true) => query.OrderBy(c => c.AuthorName),
            (1, false) => query.OrderByDescending(c => c.AuthorName),
            (2, true) => query.OrderBy(c => c.Body),
            (2, false) => query.OrderByDescending(c => c.Body),
            (3, true) => query.OrderBy(c => c.Post.Title),
            (3, false) => query.OrderByDescending(c => c.Post.Title),
            (4, true) => query.OrderBy(c => c.CreatedAtUtc),
            (4, false) => query.OrderByDescending(c => c.CreatedAtUtc),
            (5, true) => query.OrderBy(c => c.Status),
            (5, false) => query.OrderByDescending(c => c.Status),
            _ => query.OrderByDescending(c => c.CreatedAtUtc)
        };

        var page = await query.Skip(req.Start).Take(req.Length).ToListAsync();
        var token = GetAntiforgeryToken();

        var rows = page.Select((c, i) =>
        {
            var authorLabel = System.Net.WebUtility.HtmlEncode(c.AuthorName);
            if (c.IsGuest) authorLabel += " <span class=\"text-muted-dark small\">(guest)</span>";
            if (c.IsPinned) authorLabel += " <span class=\"status-pill sticky\">pin</span>";
            if (c.SpamScore > 0)
                authorLabel += $" <span class=\"ltr-field small text-muted-dark\">s{c.SpamScore}</span>";

            return new object[]
            {
                req.Start + i + 1,
                authorLabel,
                System.Net.WebUtility.HtmlEncode(c.Body.Length > 200 ? c.Body[..200] + "…" : c.Body),
                $"<a href=\"/post/{System.Net.WebUtility.HtmlEncode(c.Post.Slug)}\" dir=\"auto\">{System.Net.WebUtility.HtmlEncode(c.Post.Title)}</a>",
                PersianDate.DateTime(c.CreatedAtUtc),
                CommentStatusHtml(c.Status),
                CommentActionsHtml(c.Id, c.Status, c.IsPinned, status, token)
            };
        }).ToList();

        return Json(DataTablesResponse.Ok(req.Draw, total, filtered, rows));
    }

    private string CommentStatusHtml(CommentStatus s) => s switch
    {
        CommentStatus.Approved => $"<span class=\"status-pill approved\">{System.Net.WebUtility.HtmlEncode(_t["status.approved"])}</span>",
        CommentStatus.Rejected => $"<span class=\"status-pill rejected\">{System.Net.WebUtility.HtmlEncode(_t["status.rejected"])}</span>",
        CommentStatus.Spam => "<span class=\"status-pill rejected\">Spam</span>",
        _ => $"<span class=\"status-pill pending\">{System.Net.WebUtility.HtmlEncode(_t["status.pending"])}</span>"
    };

    private string CommentActionsHtml(int id, CommentStatus status, bool isPinned, string returnStatus, string token)
    {
        var rs = System.Net.WebUtility.HtmlEncode(returnStatus);
        var html = "<div class=\"d-flex gap-1 flex-wrap\">";
        if (status != CommentStatus.Approved)
        {
            html += $"<form method=\"post\" action=\"/Admin/ApproveComment\" class=\"d-inline\">" +
                    $"<input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"{token}\" />" +
                    $"<input type=\"hidden\" name=\"id\" value=\"{id}\" />" +
                    $"<input type=\"hidden\" name=\"returnStatus\" value=\"{rs}\" />" +
                    $"<button type=\"submit\" class=\"icon-btn approve\">{System.Net.WebUtility.HtmlEncode(_t["btn.approve"])}</button></form>";
        }
        if (status != CommentStatus.Rejected)
        {
            html += $"<form method=\"post\" action=\"/Admin/RejectComment\" class=\"d-inline\">" +
                    $"<input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"{token}\" />" +
                    $"<input type=\"hidden\" name=\"id\" value=\"{id}\" />" +
                    $"<input type=\"hidden\" name=\"returnStatus\" value=\"{rs}\" />" +
                    $"<button type=\"submit\" class=\"icon-btn reject\">{System.Net.WebUtility.HtmlEncode(_t["btn.reject"])}</button></form>";
        }
        if (status != CommentStatus.Spam)
        {
            html += $"<form method=\"post\" action=\"/Admin/MarkSpamComment\" class=\"d-inline\">" +
                    $"<input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"{token}\" />" +
                    $"<input type=\"hidden\" name=\"id\" value=\"{id}\" />" +
                    $"<input type=\"hidden\" name=\"returnStatus\" value=\"{rs}\" />" +
                    "<button type=\"submit\" class=\"icon-btn reject\">Spam</button></form>";
        }
        if (isPinned)
        {
            html += $"<form method=\"post\" action=\"/Admin/UnpinComment\" class=\"d-inline\">" +
                    $"<input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"{token}\" />" +
                    $"<input type=\"hidden\" name=\"id\" value=\"{id}\" />" +
                    $"<input type=\"hidden\" name=\"returnStatus\" value=\"{rs}\" />" +
                    "<button type=\"submit\" class=\"icon-btn\">Unpin</button></form>";
        }
        else
        {
            html += $"<form method=\"post\" action=\"/Admin/PinComment\" class=\"d-inline\">" +
                    $"<input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"{token}\" />" +
                    $"<input type=\"hidden\" name=\"id\" value=\"{id}\" />" +
                    $"<input type=\"hidden\" name=\"returnStatus\" value=\"{rs}\" />" +
                    "<button type=\"submit\" class=\"icon-btn\">Pin</button></form>";
        }
        var confirm = System.Net.WebUtility.HtmlEncode(_t["msg.confirm_delete_comment"]);
        html += $"<form method=\"post\" action=\"/Admin/DeleteComment\" class=\"d-inline\" data-confirm=\"{confirm}\">" +
                $"<input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"{token}\" />" +
                $"<input type=\"hidden\" name=\"id\" value=\"{id}\" />" +
                $"<input type=\"hidden\" name=\"returnStatus\" value=\"{rs}\" />" +
                $"<button type=\"submit\" class=\"icon-btn\">{System.Net.WebUtility.HtmlEncode(_t["btn.delete"])}</button></form></div>";
        return html;
    }

    private string GetAntiforgeryToken()
    {
        var af = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>();
        return af.GetAndStoreTokens(HttpContext).RequestToken ?? "";
    }
}
