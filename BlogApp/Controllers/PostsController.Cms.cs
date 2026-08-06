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

    private async Task LoadTaxonomyPickListsAsync(PostEditViewModel vm, int? postId = null)
    {
        vm.AvailableCategories = await GetCategoryOptionsAsync();

        var userId = AuthorAccess.UserId(User);
        var isSuper = AuthorAccess.IsSuperAdmin(User);
        var foldersQ = _db.PostFolders.AsNoTracking().AsQueryable();
        if (!isSuper && userId is not null)
            foldersQ = foldersQ.Where(f => f.OwnerUserId == userId);

        vm.AvailableFolders = await foldersQ
            .OrderBy(f => f.DisplayOrder).ThenBy(f => f.Name)
            .Select(f => new TaxonomyPickItem { Id = f.Id, Name = f.Name, Extra = f.Color })
            .ToListAsync();

        vm.AvailableTags = await _db.Tags.AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new TaxonomyPickItem { Id = t.Id, Name = t.Name })
            .Take(200)
            .ToListAsync();

        vm.AvailableSeries = await _db.PostSeries.AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new TaxonomyPickItem { Id = s.Id, Name = s.Name })
            .ToListAsync();

        vm.AvailableTopics = await _db.TopicCollections.AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new TaxonomyPickItem { Id = t.Id, Name = t.Name })
            .ToListAsync();

        if (postId is int pid && pid > 0)
        {
            vm.FolderIds = await _db.PostFolderItems.AsNoTracking()
                .Where(i => i.PostId == pid)
                .Select(i => i.FolderId)
                .ToListAsync();
            vm.SeriesId = await _db.SeriesPosts.AsNoTracking()
                .Where(sp => sp.PostId == pid)
                .Select(sp => (int?)sp.SeriesId)
                .FirstOrDefaultAsync();
        }
    }

    private async Task ApplyFoldersAndSeriesAsync(Post post, PostEditViewModel vm)
    {
        var selectedFolders = (vm.FolderIds ?? new List<int>()).Where(id => id > 0).Distinct().ToList();

        var existingFolderRows = await _db.PostFolderItems.AsTracking()
            .Where(i => i.PostId == post.Id)
            .ToListAsync();
        var existingFolderIds = existingFolderRows.Select(r => r.FolderId).ToHashSet();

        foreach (var row in existingFolderRows.Where(r => !selectedFolders.Contains(r.FolderId)))
            _db.PostFolderItems.Remove(row);

        foreach (var fid in selectedFolders.Where(id => !existingFolderIds.Contains(id)))
        {
            var folderOk = await _db.PostFolders.AsNoTracking().AnyAsync(f => f.Id == fid);
            if (!folderOk) continue;
            var max = await _db.PostFolderItems.Where(i => i.FolderId == fid).MaxAsync(i => (int?)i.SortOrder) ?? 0;
            _db.PostFolderItems.Add(new PostFolderItem
            {
                FolderId = fid,
                PostId = post.Id,
                SortOrder = max + 1,
                AddedAtUtc = DateTime.UtcNow
            });
        }

        var existingSeries = await _db.SeriesPosts.AsTracking()
            .Where(sp => sp.PostId == post.Id)
            .ToListAsync();
        _db.SeriesPosts.RemoveRange(existingSeries);

        if (vm.SeriesId is int sid && sid > 0
            && await _db.PostSeries.AsNoTracking().AnyAsync(s => s.Id == sid))
        {
            var max = await _db.SeriesPosts.Where(sp => sp.SeriesId == sid).MaxAsync(sp => (int?)sp.SortOrder) ?? 0;
            _db.SeriesPosts.Add(new SeriesPost
            {
                SeriesId = sid,
                PostId = post.Id,
                SortOrder = max + 1
            });
        }

        await _db.SaveChangesAsync();
    }

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
