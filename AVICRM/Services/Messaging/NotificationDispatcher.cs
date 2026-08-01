using AVICRM.Data;
using AVICRM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AVICRM.Services.Messaging;

public interface INotificationDispatcher
{
    Task<int> DispatchCampaignAsync(NotificationCampaign campaign, CancellationToken ct = default);
    Task<int> ProcessDueCampaignsAsync(CancellationToken ct = default);
    Task NotifyFollowersOfNewPostAsync(Post post, CancellationToken ct = default);
}

/// <summary>
/// Resolves audience → creates AppNotification rows → publishes NotificationDeliveredEvent
/// (Channel + optional RabbitMQ) for realtime SSE.
/// </summary>
public sealed class NotificationDispatcher : INotificationDispatcher
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly INotificationEventBus _bus;
    private readonly ILogger<NotificationDispatcher> _log;

    public NotificationDispatcher(
        ApplicationDbContext db,
        UserManager<ApplicationUser> users,
        INotificationEventBus bus,
        ILogger<NotificationDispatcher> logger)
    {
        _db = db;
        _users = users;
        _bus = bus;
        _log = logger;
    }

    public async Task<int> ProcessDueCampaignsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var due = await _db.NotificationCampaigns
            .Where(c => !c.IsSent && c.ScheduledAtUtc != null && c.ScheduledAtUtc <= now)
            .OrderBy(c => c.ScheduledAtUtc)
            .Take(20)
            .ToListAsync(ct);

        var total = 0;
        foreach (var c in due)
            total += await DispatchCampaignAsync(c, ct);
        return total;
    }

    public async Task NotifyFollowersOfNewPostAsync(Post post, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(post.AuthorId) || !post.IsPublished) return;

        var campaign = new NotificationCampaign
        {
            Title = post.Title,
            Body = string.IsNullOrWhiteSpace(post.Summary)
                ? null
                : (post.Summary.Length > 180 ? post.Summary[..180] + "…" : post.Summary),
            LinkUrl = "/post/" + post.Slug,
            Kind = NotificationKind.NewPost,
            Audience = NotificationAudience.AuthorFollowers,
            AuthorUserId = post.AuthorId,
            CreatedByUserId = post.AuthorId,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.NotificationCampaigns.Add(campaign);
        await _db.SaveChangesAsync(ct);
        await DispatchCampaignAsync(campaign, ct);
    }

    public async Task<int> DispatchCampaignAsync(NotificationCampaign campaign, CancellationToken ct = default)
    {
        if (campaign.IsSent) return campaign.RecipientCount;

        var recipients = await ResolveRecipientsAsync(campaign, ct);
        var count = 0;

        foreach (var userId in recipients)
        {
            var prefs = await GetOrCreatePrefsAsync(userId, ct);
            if (!prefs.InAppEnabled) continue;

            if (campaign.Kind == NotificationKind.NewPost && !prefs.NotifyNewPostFromFollowed)
                continue;

            var row = new AppNotification
            {
                UserId = userId,
                Kind = campaign.Kind,
                Title = campaign.Title,
                Body = campaign.Body,
                LinkUrl = campaign.LinkUrl,
                CampaignId = campaign.Id,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.AppNotifications.Add(row);
            await _db.SaveChangesAsync(ct);
            count++;

            await _bus.PublishAsync(new NotificationDeliveredEvent(
                row.Id, userId, row.Kind, row.Title, row.Body, row.LinkUrl, row.CreatedAtUtc), ct);
        }

        campaign.IsSent = true;
        campaign.SentAtUtc = DateTime.UtcNow;
        campaign.RecipientCount = count;
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("Campaign {Id} delivered to {Count} users Audience={Audience}",
            campaign.Id, count, campaign.Audience);
        return count;
    }

    private async Task<List<string>> ResolveRecipientsAsync(NotificationCampaign c, CancellationToken ct)
    {
        switch (c.Audience)
        {
            case NotificationAudience.SingleUser:
                return string.IsNullOrEmpty(c.TargetUserId)
                    ? new List<string>()
                    : new List<string> { c.TargetUserId };

            case NotificationAudience.UserList:
                return (c.TargetUserIdsCsv ?? "")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

            case NotificationAudience.Broadcast:
                return await _db.Users.AsNoTracking().Select(u => u.Id).ToListAsync(ct);

            case NotificationAudience.AllAuthors:
            {
                var authors = await _users.GetUsersInRoleAsync(AppRoles.Author);
                var supers = await _users.GetUsersInRoleAsync(AppRoles.SuperAdmin);
                return authors.Concat(supers).Select(u => u.Id).Distinct().ToList();
            }

            case NotificationAudience.AuthorFollowers:
                if (string.IsNullOrEmpty(c.AuthorUserId)) return new();
                return await _db.AuthorFollows.AsNoTracking()
                    .Where(f => f.AuthorUserId == c.AuthorUserId)
                    .Select(f => f.FollowerUserId)
                    .ToListAsync(ct);

            case NotificationAudience.CategoryReaders:
            {
                // Readers who viewed/bookmarked posts in this category, plus authors of those posts
                if (c.CategoryId is null) return new();
                var postIds = await _db.Posts.AsNoTracking()
                    .Where(p => p.CategoryId == c.CategoryId && !p.IsDeleted)
                    .Select(p => p.Id)
                    .ToListAsync(ct);

                var fromBookmarks = await _db.PostBookmarks.AsNoTracking()
                    .Where(b => postIds.Contains(b.PostId))
                    .Select(b => b.UserId)
                    .Distinct()
                    .ToListAsync(ct);

                var fromAuthors = await _db.Posts.AsNoTracking()
                    .Where(p => p.CategoryId == c.CategoryId && !p.IsDeleted)
                    .Select(p => p.AuthorId)
                    .Distinct()
                    .ToListAsync(ct);

                return fromBookmarks.Concat(fromAuthors).Distinct().ToList();
            }

            default:
                return new();
        }
    }

    private async Task<NotificationPreference> GetOrCreatePrefsAsync(string userId, CancellationToken ct)
    {
        var prefs = await _db.NotificationPreferences.FindAsync(new object[] { userId }, ct);
        if (prefs is not null) return prefs;
        prefs = new NotificationPreference { UserId = userId };
        _db.NotificationPreferences.Add(prefs);
        await _db.SaveChangesAsync(ct);
        return prefs;
    }
}
