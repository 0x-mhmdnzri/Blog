using System.Text.RegularExpressions;
using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services.Messaging;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Services;

/// <summary>
/// Parses @username mentions from comment bodies, persists UserMention rows,
/// and fires async notifications (in-app + push + optional email) via INotificationService
/// which publishes on the notification event bus (pub/sub).
/// </summary>
public partial class MentionsService
{
    private static readonly Regex MentionRegex = MyRegex();

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly INotificationService _notify;
    private readonly ILogger<MentionsService> _logger;

    public MentionsService(
        ApplicationDbContext db,
        UserManager<ApplicationUser> users,
        INotificationService notify,
        ILogger<MentionsService> logger)
    {
        _db = db;
        _users = users;
        _notify = notify;
        _logger = logger;
    }

    public async Task ProcessCommentMentionsAsync(
        string body,
        string actorUserId,
        int postId,
        int? commentId,
        string? postSlug = null,
        string? languageCode = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(body) || string.IsNullOrEmpty(actorUserId))
            return;

        var names = MentionRegex.Matches(body)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();

        if (names.Count == 0) return;

        var namesLower = names.Select(n => n.ToLowerInvariant()).ToHashSet();
        var candidates = await _users.Users
            .AsNoTracking()
            .Where(u => u.UserName != null)
            .Select(u => new { u.Id, u.UserName })
            .ToListAsync(ct);
        var users = candidates
            .Where(u => namesLower.Contains(u.UserName!.ToLowerInvariant()))
            .ToList();

        if (users.Count == 0) return;

        var actorName = await _users.Users.AsNoTracking()
            .Where(u => u.Id == actorUserId)
            .Select(u => u.DisplayName != null && u.DisplayName != "" ? u.DisplayName : u.UserName)
            .FirstOrDefaultAsync(ct) ?? "کاربر";

        var lang = string.IsNullOrWhiteSpace(languageCode) ? "fa" : languageCode.Trim();
        var link = string.IsNullOrEmpty(postSlug)
            ? $"/{lang}/post/{postId}"
            : $"/{lang}/post/{postSlug}";
        if (commentId is int cid)
            link += $"#comment-{cid}";

        foreach (var u in users)
        {
            if (u.Id == actorUserId) continue;

            var exists = await _db.UserMentions.AsNoTracking()
                .AnyAsync(m => m.CommentId == commentId
                               && m.MentionedUserId == u.Id
                               && m.ActorUserId == actorUserId, ct);
            if (exists) continue;

            _db.UserMentions.Add(new UserMention
            {
                MentionedUserId = u.Id,
                ActorUserId = actorUserId,
                PostId = postId,
                CommentId = commentId,
                CreatedAtUtc = DateTime.UtcNow
            });

            ActivityWriter.Write(_db, actorUserId, ActivityKind.Mention, postId: postId,
                targetUserId: u.Id,
                title: $"{actorName} از شما نام برد",
                linkUrl: link);

            try
            {
                // Async pub/sub: NotifyAsync → AppNotification + INotificationEventBus.Publish
                // → SSE/SignalR consumer → browser; WebPush if prefs.PushEnabled
                await _notify.NotifyAsync(
                    u.Id,
                    NotificationKind.Mention,
                    "شما را منشن کردند",
                    $"{actorName} در یک دیدگاه با @{u.UserName} از شما نام برد.",
                    link,
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Mention notify failed MentionedUserId={UserId}", u.Id);
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    [GeneratedRegex(@"(?:^|[\s\u200c])@([a-zA-Z0-9._\-]{2,32})\b", RegexOptions.Compiled)]
    private static partial Regex MyRegex();
}

public static class ActivityWriter
{
    public static void Write(
        ApplicationDbContext db,
        string actorId,
        ActivityKind kind,
        int? postId = null,
        int? categoryId = null,
        string? targetUserId = null,
        string? title = null,
        string? linkUrl = null,
        string? meta = null)
    {
        db.UserActivities.Add(new UserActivity
        {
            ActorUserId = actorId,
            Kind = kind,
            PostId = postId,
            CategoryId = categoryId,
            TargetUserId = targetUserId,
            Title = title,
            LinkUrl = linkUrl,
            Meta = meta,
            CreatedAtUtc = DateTime.UtcNow
        });
    }
}
