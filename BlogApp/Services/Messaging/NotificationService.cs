using BlogApp.Data;
using BlogApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BlogApp.Services.Messaging;

public class DigestOptions
{
    public bool Enabled { get; set; } = true;
    /// <summary>DayOfWeek 0=Sunday … 6=Saturday (UTC).</summary>
    public int DayOfWeekUtc { get; set; } = 1;
    public int HourUtc { get; set; } = 8;
}

public interface INotificationService
{
    Task NotifyAsync(
        string userId,
        NotificationKind kind,
        string title,
        string? body = null,
        string? linkUrl = null,
        CancellationToken ct = default);

    Task NotifyNewCommentAsync(Post post, Comment comment, CancellationToken ct = default);
    Task NotifyNewFollowerAsync(string authorUserId, ApplicationUser follower, CancellationToken ct = default);
    Task EnqueueAdSmsAsync(string phoneE164, string message, string? userId = null, CancellationToken ct = default);
    Task EnqueueAdEmailAsync(string to, string subject, string htmlBody, string? userId = null, CancellationToken ct = default);
}

public sealed class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly IEmailSender _email;
    private readonly ISmsSender _sms;
    private readonly IPushSender _push;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ApplicationDbContext db,
        IEmailSender email,
        ISmsSender sms,
        IPushSender push,
        ILogger<NotificationService> logger)
    {
        _db = db;
        _email = email;
        _sms = sms;
        _push = push;
        _logger = logger;
    }

    public async Task NotifyAsync(
        string userId,
        NotificationKind kind,
        string title,
        string? body = null,
        string? linkUrl = null,
        CancellationToken ct = default)
    {
        var prefs = await GetOrCreatePrefsAsync(userId, ct);

        if (prefs.InAppEnabled)
        {
            _db.AppNotifications.Add(new AppNotification
            {
                UserId = userId,
                Kind = kind,
                Title = title,
                Body = body,
                LinkUrl = linkUrl,
                CreatedAtUtc = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
        }

        if (prefs.PushEnabled)
            await _push.SendAsync(userId, title, body ?? "", linkUrl, ct);

        if (prefs.EmailEnabled)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user?.Email is { Length: > 0 })
            {
                try
                {
                    var html = $"<p>{System.Net.WebUtility.HtmlEncode(body ?? title)}</p>" +
                               (string.IsNullOrEmpty(linkUrl) ? "" : $"<p><a href=\"{linkUrl}\">مشاهده</a></p>");
                    await _email.SendAsync(user.Email, title, html, true, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Email notify failed UserId={UserId}", userId);
                }
            }
        }
    }

    public async Task NotifyNewCommentAsync(Post post, Comment comment, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(post.AuthorId)) return;

        var prefs = await GetOrCreatePrefsAsync(post.AuthorId, ct);
        if (!prefs.NotifyNewComment) return;

        await NotifyAsync(
            post.AuthorId,
            NotificationKind.NewComment,
            $"دیدگاه جدید روی «{post.Title}»",
            $"{comment.AuthorName}: {(comment.Body.Length > 120 ? comment.Body[..120] + "…" : comment.Body)}",
            "/Admin/Comments",
            ct);
    }

    public async Task NotifyNewFollowerAsync(string authorUserId, ApplicationUser follower, CancellationToken ct = default)
    {
        var prefs = await GetOrCreatePrefsAsync(authorUserId, ct);
        if (!prefs.NotifyNewFollower) return;

        await NotifyAsync(
            authorUserId,
            NotificationKind.NewFollower,
            "دنبال‌کننده جدید",
            $"{follower.DisplayName} شما را دنبال کرد.",
            $"/author/{follower.UserName}",
            ct);
    }

    public async Task EnqueueAdSmsAsync(string phoneE164, string message, string? userId = null, CancellationToken ct = default)
    {
        _db.OutboundMessages.Add(new OutboundMessage
        {
            Channel = "sms",
            To = phoneE164,
            Body = message,
            UserId = userId,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        await _sms.SendAsync(phoneE164, message, ct);
    }

    public async Task EnqueueAdEmailAsync(string to, string subject, string htmlBody, string? userId = null, CancellationToken ct = default)
    {
        _db.OutboundMessages.Add(new OutboundMessage
        {
            Channel = "email",
            To = to,
            Subject = subject,
            Body = htmlBody,
            IsHtml = true,
            UserId = userId,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        await _email.SendAsync(to, subject, htmlBody, true, ct);
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
