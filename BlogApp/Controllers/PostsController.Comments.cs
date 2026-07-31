using System.Security.Cryptography;
using System.Text;
using BlogApp.Models;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BlogApp.Controllers;

public partial class PostsController
{
    /// <summary>
    /// Guest + authenticated comments. Honeypot field "website" must be empty.
    /// Supports Twitter-style threaded replies via parentId (max depth).
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken, AllowAnonymous]
    [EnableRateLimiting("comment")]
    public async Task<IActionResult> AddComment(
        int postId,
        string authorName,
        string body,
        string? authorEmail = null,
        int? parentId = null,
        string? website = null)
    {
        var spamOpt = HttpContext.RequestServices.GetRequiredService<IOptions<CommentSpamOptions>>().Value;
        var spamSvc = HttpContext.RequestServices.GetRequiredService<ICommentSpamService>();

        // Honeypot — bots fill hidden "website"
        if (!string.IsNullOrWhiteSpace(website))
        {
            _logger.LogWarning("Comment honeypot tripped PostId={PostId}", postId);
            TempData["CommentSubmitted"] = "ممنون — دیدگاه شما ثبت شد.";
            var bait = await _db.Posts.AsNoTracking()
                .Where(p => p.Id == postId).Select(p => new { p.Slug, p.LanguageCode }).FirstOrDefaultAsync();
            return bait is null ? NotFound() : Redirect($"/{bait.LanguageCode}/post/{bait.Slug}#comments");
        }

        authorName = (authorName ?? string.Empty).Trim();
        body = (body ?? string.Empty).Trim();
        authorEmail = string.IsNullOrWhiteSpace(authorEmail) ? null : authorEmail.Trim();

        if (authorName.Length < 2 || authorName.Length > 80 ||
            body.Length < 2 || body.Length > spamOpt.MaxBodyLength)
        {
            TempData["CommentSubmitted"] = "نام یا متن دیدگاه معتبر نیست.";
            var bad = await _db.Posts.AsNoTracking()
                .Where(p => p.Id == postId).Select(p => new { p.Slug, p.LanguageCode }).FirstOrDefaultAsync();
            return bad is null ? NotFound() : Redirect($"/{bad.LanguageCode}/post/{bad.Slug}#comments");
        }

        authorName = new string(authorName.Where(c => !char.IsControl(c)).ToArray());
        body = new string(body.Where(c => c is '\n' or '\r' or '\t' || !char.IsControl(c)).ToArray());

        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == postId && p.IsPublished && !p.IsDeleted);
        if (post is null)
            return NotFound();

        var isAuth = User.Identity?.IsAuthenticated == true;
        var userId = isAuth ? AuthorAccess.UserId(User) : null;

        if (!isAuth && !spamOpt.GuestCommentsEnabled)
        {
            TempData["CommentSubmitted"] = "برای ارسال دیدگاه وارد شوید.";
            return RedirectToAction("Login", "Account", new { returnUrl = $"/{post.LanguageCode}/post/{post.Slug}#comments" });
        }

        if (isAuth && !string.IsNullOrEmpty(userId))
        {
            var appUser = await _db.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.DisplayName, u.Email })
                .FirstOrDefaultAsync();
            if (appUser is not null)
            {
                if (string.IsNullOrWhiteSpace(authorName))
                    authorName = appUser.DisplayName;
                authorEmail ??= appUser.Email;
            }
        }

        // Twitter-style threading: validate parent + max depth
        var maxDepth = Math.Clamp(spamOpt.MaxReplyDepth, 1, 12);
        if (parentId is int pid)
        {
            var parent = await _db.Comments.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == pid && c.PostId == postId && c.Status == CommentStatus.Approved);
            if (parent is null)
            {
                parentId = null;
            }
            else
            {
                var depth = 1;
                var walk = parent.ParentId;
                while (walk is int wid && depth < maxDepth + 2)
                {
                    depth++;
                    walk = await _db.Comments.AsNoTracking()
                        .Where(c => c.Id == wid)
                        .Select(c => c.ParentId)
                        .FirstOrDefaultAsync();
                }
                if (depth >= maxDepth)
                {
                    // Flatten: attach to the nearest allowed ancestor (Twitter-style "continue thread")
                    parentId = parent.ParentId ?? parent.Id;
                    if (depth > maxDepth)
                        parentId = parent.Id;
                }
            }
        }

        var isGuest = string.IsNullOrEmpty(userId);
        var spam = spamSvc.Evaluate(authorName, body, authorEmail, isGuest);

        var status = CommentStatus.Pending;
        if (spam.IsSpam)
            status = CommentStatus.Spam;
        else if (isAuth && spamOpt.AutoApproveAuthenticated)
            status = CommentStatus.Approved;

        var comment = new Comment
        {
            PostId = postId,
            ParentId = parentId,
            UserId = userId,
            AuthorName = authorName,
            AuthorEmail = authorEmail,
            IsGuest = isGuest,
            Body = body,
            Status = status,
            SpamScore = spam.Score,
            SpamReasons = spam.Reasons.Count > 0 ? string.Join(",", spam.Reasons) : null,
            IpHash = HashIp(HttpContext.Connection.RemoteIpAddress?.ToString()),
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();

        TempData["CommentSubmitted"] = status switch
        {
            CommentStatus.Spam => "دیدگاه شما ثبت شد و در صف بررسی است.",
            CommentStatus.Approved => "دیدگاه شما منتشر شد.",
            _ => "ممنون — دیدگاه شما در انتظار بررسی است."
        };

        if (status != CommentStatus.Spam)
        {
            try
            {
                await _notify.NotifyNewCommentAsync(post, comment);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Notify new comment failed PostId={PostId}", postId);
            }

            if (!string.IsNullOrEmpty(userId))
            {
                try
                {
                    var mentions = HttpContext.RequestServices.GetRequiredService<MentionsService>();
                    await mentions.ProcessCommentMentionsAsync(body, userId, postId, comment.Id, post.Slug);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Mentions failed CommentId={Id}", comment.Id);
                }
            }
        }

        _broadcaster.Publish(new
        {
            type = "comment",
            status = status.ToString().ToLowerInvariant(),
            postId,
            postTitle = post.Title,
            authorId = post.AuthorId,
            authorName = comment.AuthorName,
            spamScore = comment.SpamScore,
            parentId = comment.ParentId
        });

        var anchor = status == CommentStatus.Approved ? $"#comment-{comment.Id}" : "#comments";
        return Redirect($"/{post.LanguageCode}/post/{post.Slug}{anchor}");
    }

    private static string? HashIp(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return null;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(ip));
        return Convert.ToHexString(hash.AsSpan(0, 16));
    }
}
