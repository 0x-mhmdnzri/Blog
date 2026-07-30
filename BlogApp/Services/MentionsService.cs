using System.Text.RegularExpressions;
using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services.Messaging;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Services;

public partial class MentionsService
{
    private static readonly Regex MentionRegex = MyRegex();

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly INotificationService _notify;

    public MentionsService(ApplicationDbContext db, UserManager<ApplicationUser> users, INotificationService notify)
    {
        _db = db;
        _users = users;
        _notify = notify;
    }

    public async Task ProcessCommentMentionsAsync(string body, string actorUserId, int postId, int? commentId, string? postSlug = null)
    {
        if (string.IsNullOrWhiteSpace(body)) return;

        var names = MentionRegex.Matches(body)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

        if (names.Count == 0) return;

        var users = await _users.Users
            .AsNoTracking()
            .Where(u => names.Contains(u.UserName!))
            .Select(u => new { u.Id, u.UserName })
            .ToListAsync();

        var link = string.IsNullOrEmpty(postSlug) ? $"/post/{postId}" : $"/post/{postSlug}";

        foreach (var u in users)
        {
            if (u.Id == actorUserId) continue;

            _db.UserMentions.Add(new UserMention
            {
                MentionedUserId = u.Id,
                ActorUserId = actorUserId,
                PostId = postId,
                CommentId = commentId,
                CreatedAtUtc = DateTime.UtcNow
            });

            ActivityWriter.Write(_db, actorUserId, ActivityKind.Mention, postId: postId,
                targetUserId: u.Id, title: "از شما در یک دیدگاه نام برد", linkUrl: link);

            await _notify.NotifyAsync(u.Id, NotificationKind.System,
                "منشن شدید",
                $"کاربری شما را با @{u.UserName} خطاب کرد.",
                link);
        }

        await _db.SaveChangesAsync();
    }

    [GeneratedRegex(@"(?:^|\s)@([a-zA-Z0-9._-]{2,32})\b", RegexOptions.Compiled)]
    private static partial Regex MyRegex();
}

public static class ActivityWriter
{
    public static void Write(ApplicationDbContext db, string actorId, ActivityKind kind,
        int? postId = null, int? categoryId = null, string? targetUserId = null,
        string? title = null, string? linkUrl = null, string? meta = null)
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
