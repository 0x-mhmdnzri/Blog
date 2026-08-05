using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public partial class AdminController
{
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
}
