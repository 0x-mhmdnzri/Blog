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

        var filtered = await query.CountAsync();

        // Columns: 0 # (no sort), 1 title, 2 author?, 3 category, 4 status, 5 features, 6 views, 7 comments, 8 date, 9 actions
        // When !seeAll: 0 #, 1 title, 2 category, 3 status, 4 features, 5 views, 6 comments, 7 date, 8 actions
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
        var rows = page.Select((p, i) =>
        {
            var idx = req.Start + i + 1;
            var statusHtml = StatusPill(p.IsDeleted, p.IsPublished, p.ScheduledPublishAtUtc);
            var featuresHtml = FeaturesHtml(p.IsFeatured, p.IsSticky);
            var actionsHtml = PostActionsHtml(p.Id, p.IsDeleted, token);
            var titleHtml = $"<a href=\"/post/{System.Net.WebUtility.HtmlEncode(p.Slug)}\" dir=\"auto\">{System.Net.WebUtility.HtmlEncode(p.Title)}</a>";

            if (seeAll)
            {
                return new object[]
                {
                    idx,
                    titleHtml,
                    System.Net.WebUtility.HtmlEncode(p.AuthorDisplayName),
                    System.Net.WebUtility.HtmlEncode(p.CategoryName ?? "—"),
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
                System.Net.WebUtility.HtmlEncode(p.CategoryName ?? "—"),
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

    private static IQueryable<Post> ApplyPostsOrder(IQueryable<Post> query, DataTablesRequest req, bool seeAll)
    {
        // Map DT column index → field (skip # = 0)
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

    private static string StatusPill(bool deleted, bool published, DateTime? scheduled)
    {
        if (deleted) return "<span class=\"status-pill rejected\">حذف‌شده</span>";
        if (scheduled.HasValue && !published) return "<span class=\"status-pill scheduled\">زمان‌بندی</span>";
        if (published) return "<span class=\"status-pill published\">منتشرشده</span>";
        return "<span class=\"status-pill draft\">پیش‌نویس</span>";
    }

    private static string FeaturesHtml(bool featured, bool sticky)
    {
        if (!featured && !sticky) return "<span class=\"text-muted-dark small\">—</span>";
        var parts = new List<string>();
        if (featured) parts.Add("<span class=\"status-pill featured\">ویژه</span>");
        if (sticky) parts.Add("<span class=\"status-pill sticky\">چسبان</span>");
        return $"<div class=\"d-flex gap-1 flex-wrap\">{string.Join("", parts)}</div>";
    }

    private static string PostActionsHtml(int id, bool deleted, string token)
    {
        if (deleted)
        {
            return $"<form method=\"post\" action=\"/Posts/Restore\" class=\"d-inline\">" +
                   $"<input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"{token}\" />" +
                   $"<input type=\"hidden\" name=\"id\" value=\"{id}\" />" +
                   "<button type=\"submit\" class=\"icon-btn approve\">بازگردانی</button></form>";
        }

        return $"<div class=\"d-flex gap-1 flex-wrap\">" +
               $"<a class=\"icon-btn\" href=\"/Posts/Edit/{id}\">ویرایش</a>" +
               $"<form method=\"post\" action=\"/Posts/Duplicate\" class=\"d-inline\">" +
               $"<input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"{token}\" />" +
               $"<input type=\"hidden\" name=\"id\" value=\"{id}\" />" +
               "<button type=\"submit\" class=\"icon-btn\">کپی</button></form>" +
               $"<form method=\"post\" action=\"/Posts/Delete\" class=\"d-inline\" data-confirm=\"انتقال به سطل زباله؟\">" +
               $"<input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"{token}\" />" +
               $"<input type=\"hidden\" name=\"id\" value=\"{id}\" />" +
               "<button type=\"submit\" class=\"icon-btn reject\">حذف</button></form></div>";
    }

    /// <summary>Empty shell — rows via CommentsData.</summary>
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

        var filtered = await query.CountAsync();

        // 0 #, 1 author, 2 body, 3 post, 4 date, 5 status, 6 actions
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

        var rows = page.Select((c, i) => new object[]
        {
            req.Start + i + 1,
            System.Net.WebUtility.HtmlEncode(c.AuthorName),
            System.Net.WebUtility.HtmlEncode(c.Body.Length > 200 ? c.Body[..200] + "…" : c.Body),
            $"<a href=\"/post/{System.Net.WebUtility.HtmlEncode(c.Post.Slug)}\" dir=\"auto\">{System.Net.WebUtility.HtmlEncode(c.Post.Title)}</a>",
            PersianDate.DateTime(c.CreatedAtUtc),
            CommentStatusHtml(c.Status),
            CommentActionsHtml(c.Id, c.Status, status, token)
        }).ToList();

        return Json(DataTablesResponse.Ok(req.Draw, total, filtered, rows));
    }

    private static string CommentStatusHtml(CommentStatus s) => s switch
    {
        CommentStatus.Approved => "<span class=\"status-pill approved\">تأییدشده</span>",
        CommentStatus.Rejected => "<span class=\"status-pill rejected\">ردشده</span>",
        _ => "<span class=\"status-pill pending\">در انتظار</span>"
    };

    private static string CommentActionsHtml(int id, CommentStatus status, string returnStatus, string token)
    {
        var html = "<div class=\"d-flex gap-1 flex-wrap\">";
        if (status != CommentStatus.Approved)
        {
            html += $"<form method=\"post\" action=\"/Admin/ApproveComment\" class=\"d-inline\">" +
                    $"<input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"{token}\" />" +
                    $"<input type=\"hidden\" name=\"id\" value=\"{id}\" />" +
                    $"<input type=\"hidden\" name=\"returnStatus\" value=\"{System.Net.WebUtility.HtmlEncode(returnStatus)}\" />" +
                    "<button type=\"submit\" class=\"icon-btn approve\">تأیید</button></form>";
        }
        if (status != CommentStatus.Rejected)
        {
            html += $"<form method=\"post\" action=\"/Admin/RejectComment\" class=\"d-inline\">" +
                    $"<input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"{token}\" />" +
                    $"<input type=\"hidden\" name=\"id\" value=\"{id}\" />" +
                    $"<input type=\"hidden\" name=\"returnStatus\" value=\"{System.Net.WebUtility.HtmlEncode(returnStatus)}\" />" +
                    "<button type=\"submit\" class=\"icon-btn reject\">رد</button></form>";
        }
        html += $"<form method=\"post\" action=\"/Admin/DeleteComment\" class=\"d-inline\" data-confirm=\"این دیدگاه برای همیشه حذف شود؟\">" +
                $"<input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"{token}\" />" +
                $"<input type=\"hidden\" name=\"id\" value=\"{id}\" />" +
                $"<input type=\"hidden\" name=\"returnStatus\" value=\"{System.Net.WebUtility.HtmlEncode(returnStatus)}\" />" +
                "<button type=\"submit\" class=\"icon-btn\">حذف</button></form></div>";
        return html;
    }

    private string GetAntiforgeryToken()
    {
        var af = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>();
        return af.GetAndStoreTokens(HttpContext).RequestToken ?? "";
    }
}
