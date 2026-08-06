using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public partial class AccountController
{
    [HttpGet]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> PublicProfile(
        string userName,
        string? q = null,
        string? sort = null,
        string? folder = null,
        string? category = null,
        string? tag = null,
        string? series = null,
        string? topic = null)
    {
        if (string.IsNullOrWhiteSpace(userName) || userName.Length > 64)
            return NotFound();

        var user = await _userManager.FindByNameAsync(userName);
        if (user is null) return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        var isAuthor = roles.Contains(AppRoles.Author) || roles.Contains(AppRoles.SuperAdmin);
        var isSuper = roles.Contains(AppRoles.SuperAdmin);

        var baseQuery = _db.Posts.AsNoTracking()
            .Where(p => p.AuthorId == user.Id && p.IsPublished && !p.IsDeleted);

        var postCount = await baseQuery.CountAsync();
        var totalViews = await baseQuery.SumAsync(p => (long)p.ViewCount);
        var followerCount = await _db.AuthorFollows.CountAsync(f => f.AuthorUserId == user.Id);

        // Filter option lists (from this author's published posts)
        var folders = await _db.PostFolderItems.AsNoTracking()
            .Where(i => i.Post.AuthorId == user.Id && i.Post.IsPublished && !i.Post.IsDeleted)
            .GroupBy(i => new { i.Folder.Slug, i.Folder.Name })
            .Select(g => new AuthorFilterOption {Slug = g.Key.Slug, Name = g.Key.Name, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(40)
            .ToListAsync();

        var categories = await baseQuery
            .Where(p => p.Category != null)
            .GroupBy(p => new { p.Category!.Slug, p.Category.Name })
            .Select(g => new AuthorFilterOption { Slug = g.Key.Slug, Name = g.Key.Name, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(40)
            .ToListAsync();

        var tags = await _db.PostTags.AsNoTracking()
            .Where(pt => pt.Post.AuthorId == user.Id && pt.Post.IsPublished && !pt.Post.IsDeleted)
            .GroupBy(pt => new { pt.Tag.Slug, pt.Tag.Name })
            .Select(g => new AuthorFilterOption { Slug = g.Key.Slug, Name = g.Key.Name, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(40)
            .ToListAsync();

        var seriesList = await _db.SeriesPosts.AsNoTracking()
            .Where(sp => sp.Post.AuthorId == user.Id && sp.Post.IsPublished && !sp.Post.IsDeleted)
            .GroupBy(sp => new { sp.Series.Slug, sp.Series.Name })
            .Select(g => new AuthorFilterOption {Slug = g.Key.Slug, Name = g.Key.Name, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(40)
            .ToListAsync();

        var topics = await _db.TopicCollections.AsNoTracking()
            .Where(t => t.IsPublished)
            .OrderBy(t => t.Name)
            .Select(t => new AuthorFilterOption { Slug = t.Slug, Name = t.Name, Count = t.Items.Count })
            .Take(40)
            .ToListAsync();

        var query = baseQuery;

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(p =>
                p.Title.Contains(term)
                || (p.Summary != null && p.Summary.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(folder))
        {
            var fs = folder.Trim();
            query = query.Where(p =>
                _db.PostFolderItems.Any(i => i.PostId == p.Id && i.Folder.Slug == fs));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var cs = category.Trim();
            query = query.Where(p => p.Category != null && p.Category.Slug == cs);
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            var ts = tag.Trim();
            query = query.Where(p => p.PostTags.Any(pt => pt.Tag.Slug == ts));
        }

        if (!string.IsNullOrWhiteSpace(series))
        {
            var ss = series.Trim();
            query = query.Where(p =>
                p.SeriesMemberships.Any(sp => sp.Series.Slug == ss));
        }

        if (!string.IsNullOrWhiteSpace(topic))
        {
            var tslug = topic.Trim();
            var topicRow = await _db.TopicCollections.AsNoTracking()
                .Include(t => t.Items)
                .FirstOrDefaultAsync(t => t.Slug == tslug);
            if (topicRow != null)
            {
                var catIds = topicRow.Items.Where(i => i.CategoryId != null).Select(i => i.CategoryId!.Value).ToList();
                var tagIds = topicRow.Items.Where(i => i.TagId != null).Select(i => i.TagId!.Value).ToList();
                if (catIds.Count > 0 || tagIds.Count > 0)
                {
                    query = query.Where(p =>
                        (p.CategoryId != null && catIds.Contains(p.CategoryId.Value))
                        || p.PostTags.Any(pt => tagIds.Contains(pt.TagId)));
                }
                else
                {
                    query = query.Where(_ => false);
                }
            }
        }

        sort = string.IsNullOrWhiteSpace(sort) ? "newest" : sort.Trim().ToLowerInvariant();
        query = sort switch
        {
            "oldest" => query.OrderBy(p => p.PublishedAtUtc),
            "popular" => query.OrderByDescending(p => p.ViewCount).ThenByDescending(p => p.PublishedAtUtc),
            "title" => query.OrderBy(p => p.Title),
            "read" => query.OrderByDescending(p => p.ReadingTimeMinutes),
            _ => query.OrderByDescending(p => p.IsSticky).ThenByDescending(p => p.PublishedAtUtc)
        };

        var filteredCount = await query.CountAsync();
        var posts = await query
            .Take(60)
            .Select(p => new AuthorPostItem
            {
                Title = p.Title,
                Slug = p.Slug,
                LanguageCode = p.LanguageCode,
                Summary = p.Summary,
                PublishedAtUtc = p.PublishedAtUtc,
                ViewCount = p.ViewCount,
                ReadingTimeMinutes = p.ReadingTimeMinutes,
                CategoryName = p.Category != null ? p.Category.Name : null,
                CoverUrl = p.CoverMediaAssetId != null ? "/media/" + p.CoverMediaAssetId : null
            })
            .ToListAsync();

        var viewerId = AuthorAccess.UserId(User);
        var isOwn = viewerId != null && viewerId == user.Id;
        var canFollow = viewerId != null && !isOwn
            && (User.IsInRole(AppRoles.Reader) || User.IsInRole(AppRoles.Author) || User.IsInRole(AppRoles.SuperAdmin));
        var isFollowing = canFollow
            && await _db.AuthorFollows.AnyAsync(f => f.FollowerUserId == viewerId && f.AuthorUserId == user.Id);

        ViewData["Description"] = string.IsNullOrWhiteSpace(user.Bio)
            ? $"{user.DisplayName} · @{user.UserName}"
            : user.Bio;
        ViewData["OgType"] = "profile";
        ViewData["OgImage"] = $"{Request.Scheme}://{Request.Host}/og/author/{user.Id}.png?v={postCount}-{followerCount}-{totalViews}";
        ViewData["OgImageAlt"] = user.DisplayName;

        var vm = new PublicAuthorProfileViewModel
        {
            UserId = user.Id,
            UserName = user.UserName!,
            DisplayName = user.DisplayName,
            Bio = user.Bio,
            HasProfileImage = user.ProfileImage is { Length: > 0 },
            Gender = user.Gender,
            Twitter = user.Twitter,
            LinkedIn = user.LinkedIn,
            Telegram = user.Telegram,
            Phone = user.Phone,
            Website = user.Website,
            GitHub = user.GitHub,
            Instagram = user.Instagram,
            CanFollow = canFollow,
            IsFollowing = isFollowing,
            IsOwnProfile = isOwn,
            IsAuthor = isAuthor,
            IsSuperAdmin = isSuper,
            JoinedAtUtc = user.CreatedAtUtc,
            FollowerCount = followerCount,
            PostCount = postCount,
            TotalViews = totalViews,
            Posts = posts,
            Q = q,
            Sort = sort,
            Folder = folder,
            Category = category,
            Tag = tag,
            Series = series,
            Topic = topic,
            FilteredCount = filteredCount,
            Folders = folders,
            Categories = categories,
            Tags = tags,
            SeriesList = seriesList,
            Topics = topics
        };
        return View(vm);
    }
}
