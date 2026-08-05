using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public partial class PostsController
{
    private async Task SaveRevisionAsync(Post post, string userId, string note)
    {
        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        var revision = new PostRevision
        {
            PostId = post.Id,
            Title = post.Title,
            Summary = post.Summary,
            ContentMarkdown = post.ContentMarkdown,
            CreatedAtUtc = DateTime.UtcNow,
            Note = note,
            CreatedByUserId = userId
        };
        _db.PostRevisions.Add(revision);
        var old = await _db.PostRevisions
            .Where(r => r.PostId == post.Id)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip(49)
            .ToListAsync();
        if (old.Count > 0)
            _db.PostRevisions.RemoveRange(old);
        await _db.SaveChangesAsync();
    }

    /// <summary>Request-path fallback for schedule/expire. Hosted service is primary (every 30s).</summary>
    private async Task ApplyScheduledAndExpirationAsync()
    {
        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        var now = DateTime.UtcNow;

        var toPublish = await _db.Posts
            .Where(p => !p.IsDeleted && !p.IsPublished
                        && p.ScheduledPublishAtUtc != null
                        && p.ScheduledPublishAtUtc <= now
                        && p.ReviewStatus != PostReviewStatus.PendingReview
                        && p.ReviewStatus != PostReviewStatus.Rejected)
            .ToListAsync();

        foreach (var p in toPublish)
        {
            p.IsPublished = true;
            p.PublishedAtUtc ??= now;
            p.ScheduledPublishAtUtc = null;
            p.UpdatedAtUtc = now;
        }

        var toExpire = await _db.Posts
            .Where(p => !p.IsDeleted && p.IsPublished
                        && p.ExpiresAtUtc != null
                        && p.ExpiresAtUtc <= now)
            .ToListAsync();

        foreach (var p in toExpire)
        {
            p.IsPublished = false;
            p.UpdatedAtUtc = now;
        }

        if (toPublish.Count + toExpire.Count > 0)
        {
            await _db.SaveChangesAsync();
            _logger.LogInformation("ApplyScheduled published={Pub} expired={Exp}", toPublish.Count, toExpire.Count);
        }
    }

    private async Task<List<CategoryOption>> GetCategoryOptionsAsync() =>
        await _db.Categories.OrderBy(c => c.Name)
            .Select(c => new CategoryOption { Id = c.Id, Name = c.Name })
            .ToListAsync();

    private async Task<string> MakeUniqueSlugAsync(string baseSlug, string? languageCode = null)
    {
        var lang = AppCultures.Normalize(languageCode ?? _culture.CurrentCode);
        var slug = baseSlug;
        var i = 2;
        while (await _db.Posts.AnyAsync(p => p.Slug == slug && p.LanguageCode == lang && !p.IsDeleted))
            slug = $"{baseSlug}-{i++}";
        return slug;
    }

    private async Task ApplyTagsAsync(Post post, string? tagsCsv)
    {
        if (string.IsNullOrWhiteSpace(tagsCsv)) return;
        var names = tagsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            var slug = BlogApp.Services.SlugHelper.Slugify(name);
            var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Slug == slug);
            if (tag is null)
            {
                tag = new Tag { Name = name, Slug = slug };
                _db.Tags.Add(tag);
            }
            post.PostTags.Add(new PostTag { Tag = tag, Post = post });
        }
    }

    /// <summary>Apply schedule vs immediate publish. Non–SuperAdmin authors go to PendingReview (not public).</summary>
    private void ApplyPublishState(Post post, bool wantPublish, DateTime? scheduledLocalOrUtc, DateTime? expiresLocalOrUtc, bool wasPublished)
    {
        var offset = ReadClientTimezoneOffset();
        var clientConverted = Request.Form.ContainsKey("__dt_utc_converted")
                              || string.Equals(Request.Headers["X-Dt-Utc-Converted"], "1", StringComparison.Ordinal);

        post.ExpiresAtUtc = DateTimeUserLocal.ToUtc(expiresLocalOrUtc, offset, clientConverted || offset is null);
        var scheduled = DateTimeUserLocal.ToUtc(scheduledLocalOrUtc, offset, clientConverted || offset is null);
        var isSuper = AuthorAccess.IsSuperAdmin(User);

        if (scheduled is DateTime when && when > DateTime.UtcNow)
        {
            post.IsPublished = false;
            post.ScheduledPublishAtUtc = when;
            if (!isSuper)
                post.ReviewStatus = PostReviewStatus.PendingReview;
            else if (post.ReviewStatus is PostReviewStatus.None or PostReviewStatus.PendingReview or PostReviewStatus.Rejected)
                post.ReviewStatus = PostReviewStatus.Approved;
            _logger.LogInformation(
                "PostId={Id} scheduled for {When:o} UTC (now={Now:o}, offsetMin={Off}, review={Rev})",
                post.Id, when, DateTime.UtcNow, offset, post.ReviewStatus);
        }
        else if (wantPublish)
        {
            if (!isSuper)
            {
                // Author publish request → queue for SuperAdmin; never public until approved
                post.IsPublished = false;
                post.ScheduledPublishAtUtc = null;
                post.ReviewStatus = PostReviewStatus.PendingReview;
            }
            else
            {
                post.IsPublished = true;
                post.ScheduledPublishAtUtc = null;
                if (!wasPublished)
                    post.PublishedAtUtc = DateTime.UtcNow;
                post.ReviewStatus = PostReviewStatus.Approved;
                post.ReviewNote = null;
            }
        }
        else
        {
            post.IsPublished = false;
            post.ScheduledPublishAtUtc = null;
            if (post.ReviewStatus == PostReviewStatus.PendingReview)
                post.ReviewStatus = PostReviewStatus.None;
        }
    }

    private async Task NotifySuperAdminsPendingPostAsync(Post post)
    {
        try
        {
            var superIds = await (
                from ur in _db.UserRoles
                join r in _db.Roles on ur.RoleId equals r.Id
                where r.Name == AppRoles.SuperAdmin
                select ur.UserId
            ).Distinct().ToListAsync();

            if (superIds.Count == 0) return;

            var authorLabel = User.Identity?.Name ?? post.AuthorId;
            var title = "نوشته در انتظار تأیید";
            var body = "«" + post.Title + "» توسط " + authorLabel + " برای انتشار ارسال شد.";
            var link = "/AdminModeration";

            foreach (var uid in superIds)
            {
                if (string.Equals(uid, post.AuthorId, StringComparison.Ordinal)) continue;
                await _notify.NotifyAsync(uid, NotificationKind.AdminMessage, title, body, link);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NotifySuperAdminsPendingPost failed for PostId={Id}", post.Id);
        }
    }

    /// <summary>Compat alias — older call sites used this name (CS0103 hotfix).</summary>
    private Task NotifySuperAdminsPostPendingAsync(Post post) =>
        NotifySuperAdminsPendingPostAsync(post);

    private int? ReadClientTimezoneOffset()
    {
        if (Request.Form.TryGetValue("__timezoneOffset", out var formVal)
            && int.TryParse(formVal.ToString(), out var formOff))
            return formOff;

        if (Request.Headers.TryGetValue("X-Timezone-Offset", out var hdr)
            && int.TryParse(hdr.ToString(), out var hdrOff))
            return hdrOff;

        return null;
    }
}
