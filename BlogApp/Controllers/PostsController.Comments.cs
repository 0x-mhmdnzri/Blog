using System.Security.Cryptography;
using System.Text;
using BlogApp.Models;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BlogApp.Controllers;

public partial class PostsController
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(
        int postId,
        string? authorName,
        string? authorEmail,
        string body,
        int? parentId,
        string? website)
    {
        var spamOpt = HttpContext.RequestServices.GetRequiredService<IOptions<CommentSpamOptions>>().Value;

        // Honeypot
        if (!string.IsNullOrWhiteSpace(website))
        {
            var bait = await _db.Posts.AsNoTracking()
                .Where(p => p.Id == postId).Select(p => new { p.Slug, p.LanguageCode }).FirstOrDefaultAsync();
            return bait is null ? NotFound() : Redirect($"/{bait.LanguageCode}/post/{bait.Slug}#comments");
        }

        authorName ??= "";
        body ??= "";
        authorName = authorName.Trim();
        body = body.Trim();

        if (string.IsNullOrWhiteSpace(body) || body.Length > spamOpt.MaxBodyLength || body.Length < spamOpt.MinBodyLength)
        {
            TempData["CommentSubmitted"] = "متن دیدگاه نامعتبر است.";
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
                    authorName = appUser.DisplayName ?? User.Identity?.Name ?? "کاربر";
                authorEmail ??= appUser.Email;
            }
        }

        if (string.IsNullOrWhiteSpace(authorName))
            authorName = isAuth ? (User.Identity?.Name ?? "کاربر") : "مهمان";

        var status = CommentStatus.Pending;
        if (isAuth && spamOpt.AutoApproveAuthenticated)
            status = CommentStatus.Approved;

        var spam = HttpContext.RequestServices.GetService<CommentSpamService>();
        if (spam is not null)
        {
            var score = spam.Score(body, authorName, authorEmail);
            if (score >= spamOpt.SpamThreshold)
                status = CommentStatus.Spam;
        }

        var comment = new Comment
        {
            PostId = postId,
            ParentId = parentId,
            AuthorName = authorName,
            AuthorEmail = authorEmail,
            Body = body,
            Status = status,
            UserId = userId,
            CreatedAtUtc = DateTime.UtcNow
        };

        // rate limit by IP hash
        try
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
            if (ip.Length > 0)
            {
                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ip))).ToLowerInvariant();
                comment.AuthorIpHash = hash[..Math.Min(64, hash.Length)];
            }
        }
        catch { /* ignore */ }

        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();

        if (status == CommentStatus.Approved && !string.IsNullOrEmpty(post.AuthorId))
        {
            try
            {
                var notify = HttpContext.RequestServices.GetService<BlogApp.Services.Messaging.INotificationService>();
                if (notify is not null)
                    await notify.NotifyNewCommentAsync(post, comment);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "NotifyNewComment failed CommentId={Id}", comment.Id);
            }
        }

        if (isAuth && !string.IsNullOrEmpty(userId) && status != CommentStatus.Spam)
        {
            try
            {
                var mentions = HttpContext.RequestServices.GetRequiredService<MentionsService>();
                await mentions.ProcessCommentMentionsAsync(body, userId, postId, comment.Id, post.Slug, post.LanguageCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Mentions failed CommentId={Id}", comment.Id);
            }
        }

        TempData["CommentSubmitted"] = status switch
        {
            CommentStatus.Approved => "دیدگاه شما منتشر شد.",
            CommentStatus.Spam => "دیدگاه شما به‌عنوان هرزنامه شناسایی شد.",
            _ => "دیدگاه شما ثبت شد و پس از بررسی منتشر می‌شود."
        };

        var anchor = status == CommentStatus.Approved ? $"#comment-{comment.Id}" : "#comments";
        return Redirect($"/{post.LanguageCode}/post/{post.Slug}{anchor}");
    }
}
