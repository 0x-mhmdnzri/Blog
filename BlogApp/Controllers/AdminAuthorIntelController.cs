using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

/// <summary>
/// SuperAdmin-only author intelligence panel.
/// Tracks publishing behavior, engagement, audience, risk, and health scoring.
/// </summary>
[Authorize(Roles = AppRoles.SuperAdmin)]
public class AdminAuthorIntelController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public AdminAuthorIntelController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q = null, string? sort = "health")
    {
        var today = DateTime.UtcNow.Date;
        var d30 = today.AddDays(-29);

        var authorRoleUsers = await _users.GetUsersInRoleAsync(AppRoles.Author);
        var superUsers = await _users.GetUsersInRoleAsync(AppRoles.SuperAdmin);
        var allStaff = authorRoleUsers
            .Concat(superUsers)
            .GroupBy(u => u.Id)
            .Select(g => g.First())
            .ToList();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            allStaff = allStaff.Where(u =>
                (u.UserName?.ToLowerInvariant().Contains(term) ?? false) ||
                (u.DisplayName?.ToLowerInvariant().Contains(term) ?? false) ||
                (u.Email?.ToLowerInvariant().Contains(term) ?? false)).ToList();
        }

        var userIds = allStaff.Select(u => u.Id).ToList();
        var posts = await _db.Posts.AsNoTracking()
            .Where(p => userIds.Contains(p.AuthorId))
            .Select(p => new { p.Id, p.AuthorId, p.IsDeleted, p.IsPublished, p.ViewCount, p.PublishedAtUtc, p.UpdatedAtUtc })
            .ToListAsync();

        var postIds = posts.Where(p => !p.IsDeleted).Select(p => p.Id).ToList();

        var views30 = postIds.Count == 0
            ? new List<int>()
            : await _db.PostViews.AsNoTracking()
                .Where(v => v.ViewedAtUtc >= d30 && postIds.Contains(v.PostId))
                .Select(v => v.PostId)
                .ToListAsync();

        var likeRows = postIds.Count == 0
            ? new List<int>()
            : await _db.PostLikes.AsNoTracking()
                .Where(l => postIds.Contains(l.PostId))
                .Select(l => l.PostId)
                .ToListAsync();

        var reactionRows = postIds.Count == 0
            ? new List<int>()
            : await _db.PostReactions.AsNoTracking()
                .Where(r => postIds.Contains(r.PostId))
                .Select(r => r.PostId)
                .ToListAsync();

        var engagementByPost = likeRows.Concat(reactionRows)
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());

        var commentCounts = postIds.Count == 0
            ? new Dictionary<int, int>()
            : await _db.Comments.AsNoTracking()
                .Where(c => postIds.Contains(c.PostId) && c.Status == CommentStatus.Approved)
                .GroupBy(c => c.PostId)
                .Select(g => new { PostId = g.Key, Cnt = g.Count() })
                .ToDictionaryAsync(x => x.PostId, x => x.Cnt);

        var followers = await _db.AuthorFollows.AsNoTracking()
            .Where(f => userIds.Contains(f.AuthorUserId))
            .GroupBy(f => f.AuthorUserId)
            .Select(g => new { AuthorId = g.Key, Cnt = g.Count() })
            .ToDictionaryAsync(x => x.AuthorId, x => x.Cnt);

        var following = await _db.AuthorFollows.AsNoTracking()
            .Where(f => userIds.Contains(f.FollowerUserId))
            .GroupBy(f => f.FollowerUserId)
            .Select(g => new { UserId = g.Key, Cnt = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Cnt);

        var reportsByAuthor = new Dictionary<string, int>();
        try
        {
            var openReports = await _db.ContentReports.AsNoTracking()
                .Where(r => r.Status == ContentReportStatus.Open && r.TargetType == ContentReportTarget.Post)
                .Select(r => r.TargetId)
                .ToListAsync();
            foreach (var p in posts.Where(p => !p.IsDeleted && openReports.Contains(p.Id)))
                reportsByAuthor[p.AuthorId] = reportsByAuthor.GetValueOrDefault(p.AuthorId) + 1;
        }
        catch { /* ContentReports may not be migrated yet */ }

        var cards = new List<AuthorIntelCard>();
        foreach (var u in allStaff)
        {
            var myPosts = posts.Where(p => p.AuthorId == u.Id && !p.IsDeleted).ToList();
            var published = myPosts.Where(p => p.IsPublished).ToList();
            var myPostIds = myPosts.Select(p => p.Id).ToHashSet();

            var totalViews = published.Sum(p => p.ViewCount);
            var v30 = views30.Count(id => myPostIds.Contains(id));
            var likes = myPostIds.Sum(id => engagementByPost.GetValueOrDefault(id));
            var comments = myPostIds.Sum(id => commentCounts.GetValueOrDefault(id));
            var posts30 = published.Count(p => p.PublishedAtUtc >= d30);

            var lastPub = published.OrderByDescending(p => p.PublishedAtUtc).Select(p => p.PublishedAtUtc).FirstOrDefault();
            var lastAct = myPosts.OrderByDescending(p => p.UpdatedAtUtc).Select(p => (DateTime?)p.UpdatedAtUtc).FirstOrDefault()
                          ?? lastPub;

            var (streak, longest) = ComputeStreak(
                published.Where(p => p.PublishedAtUtc.HasValue).Select(p => p.PublishedAtUtc!.Value.Date).Distinct().OrderBy(d => d).ToList(),
                today);

            var engRate = totalViews == 0 ? 0 : Math.Round((likes + comments) * 100.0 / totalViews, 2);
            var daysSince = lastPub.HasValue ? (today - lastPub.Value.Date).TotalDays : 999;
            var health = ComputeHealth(published.Count, posts30, v30, engRate, streak, reportsByAuthor.GetValueOrDefault(u.Id), daysSince);

            cards.Add(new AuthorIntelCard
            {
                UserId = u.Id,
                UserName = u.UserName ?? "",
                DisplayName = string.IsNullOrWhiteSpace(u.DisplayName) ? (u.UserName ?? "—") : u.DisplayName!,
                Bio = u.Bio,
                HasAvatar = u.ProfileImage != null && u.ProfileImage.Length > 0,
                JoinedAtUtc = u.CreatedAtUtc,
                LastPublishedAtUtc = lastPub,
                LastActiveAtUtc = lastAct,
                TotalPosts = myPosts.Count,
                PublishedPosts = published.Count,
                DraftPosts = myPosts.Count(p => !p.IsPublished),
                TotalViews = totalViews,
                Views30d = v30,
                Likes = likes,
                Comments = comments,
                Followers = followers.GetValueOrDefault(u.Id),
                Following = following.GetValueOrDefault(u.Id),
                OpenReports = reportsByAuthor.GetValueOrDefault(u.Id),
                HealthScore = health.Score,
                HealthLabel = health.Label,
                Momentum = health.Momentum,
                CurrentStreakDays = streak,
                LongestStreakDays = longest,
                EngagementRate = engRate,
                Posts30d = posts30
            });
        }

        cards = (sort?.ToLowerInvariant()) switch
        {
            "views" => cards.OrderByDescending(c => c.Views30d).ThenByDescending(c => c.TotalViews).ToList(),
            "posts" => cards.OrderByDescending(c => c.PublishedPosts).ToList(),
            "followers" => cards.OrderByDescending(c => c.Followers).ToList(),
            "engagement" => cards.OrderByDescending(c => c.EngagementRate).ToList(),
            "recent" => cards.OrderByDescending(c => c.LastPublishedAtUtc ?? DateTime.MinValue).ToList(),
            "name" => cards.OrderBy(c => c.DisplayName).ToList(),
            _ => cards.OrderByDescending(c => c.HealthScore).ThenByDescending(c => c.Views30d).ToList()
        };

        return View(new AuthorIntelIndexViewModel
        {
            TotalAuthors = cards.Count,
            ActiveAuthors = cards.Count(c => c.Momentum is "rising" or "stable" && c.Posts30d > 0),
            RisingAuthors = cards.Count(c => c.Momentum == "rising"),
            DormantAuthors = cards.Count(c => c.Momentum == "dormant"),
            Authors = cards,
            Sort = sort,
            Q = q
        });
    }

    [HttpGet]
    public async Task<IActionResult> Detail(string id, int range = 90)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();
        if (range is not (30 or 90 or 180)) range = 90;

        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();

        // Redirect to Index with notice for now — full Detail lands next push
        // Minimal detail so the route resolves
        var today = DateTime.UtcNow.Date;
        var published = await _db.Posts.AsNoTracking()
            .Where(p => p.AuthorId == id && !p.IsDeleted && p.IsPublished)
            .OrderByDescending(p => p.PublishedAtUtc)
            .Take(20)
            .ToListAsync();

        var vm = new AuthorIntelDetailViewModel
        {
            UserId = user.Id,
            UserName = user.UserName ?? "",
            DisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? (user.UserName ?? "—") : user.DisplayName!,
            Bio = user.Bio,
            Email = user.Email,
            HasAvatar = user.ProfileImage != null && user.ProfileImage.Length > 0,
            JoinedAtUtc = user.CreatedAtUtc,
            PublishedPosts = published.Count,
            TotalViews = published.Sum(p => p.ViewCount),
            HealthScore = 50,
            HealthLabel = "Neutral",
            Momentum = "stable",
            Insights = new List<string> { "Full detail charts arriving in next deploy — Index cards are live." },
            RecentPosts = published.Select(p => new AuthorIntelPostRow
            {
                Id = p.Id,
                Title = p.Title,
                Slug = p.Slug,
                LanguageCode = p.LanguageCode ?? "en",
                IsPublished = p.IsPublished,
                PublishedAtUtc = p.PublishedAtUtc,
                Views = p.ViewCount
            }).ToList()
        };

        ViewBag.Range = range;
        return View(vm);
    }

    private static (int Current, int Longest) ComputeStreak(List<DateTime> days, DateTime today)
    {
        if (days.Count == 0) return (0, 0);
        var set = days.ToHashSet();
        var longest = 0;
        var cur = 0;
        for (var d = days[0]; d <= today; d = d.AddDays(1))
        {
            if (set.Contains(d)) { cur++; longest = Math.Max(longest, cur); }
            else cur = 0;
        }
        var current = 0;
        for (var d = today; set.Contains(d); d = d.AddDays(-1)) current++;
        if (current == 0 && set.Contains(today.AddDays(-1)))
            for (var d = today.AddDays(-1); set.Contains(d); d = d.AddDays(-1)) current++;
        return (current, longest);
    }

    private static (int Score, string Label, string Momentum) ComputeHealth(
        int publishedCount, int posts30, int views30, double engRate, int streak, int openReports, double daysSinceLast)
    {
        var score = 40;
        score += Math.Min(20, publishedCount);
        score += Math.Min(15, posts30 * 3);
        score += Math.Min(15, views30 / 50);
        score += Math.Min(10, (int)(engRate * 2));
        score += Math.Min(10, streak);
        score -= Math.Min(25, openReports * 8);
        if (daysSinceLast > 60) score -= 15;
        else if (daysSinceLast > 30) score -= 8;
        score = Math.Clamp(score, 0, 100);

        var label = score >= 80 ? "Excellent" : score >= 60 ? "Good" : score >= 40 ? "Neutral" : score >= 20 ? "Weak" : "Critical";
        var momentum = daysSinceLast > 45 ? "dormant"
            : posts30 >= 3 && views30 > 0 ? "rising"
            : posts30 == 0 && daysSinceLast > 14 ? "declining"
            : "stable";
        return (score, label, momentum);
    }
}
