using BlogApp.Data;
using BlogApp.Developer.Domain;
using BlogApp.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Services.Messaging;

/// <summary>
/// MassTransit consumers that bridge domain integration events → in-app notifications,
/// push, and outbound webhooks. Fully async; no request-thread blocking.
/// </summary>
public sealed class NotifyOnPostPublishedConsumer : IConsumer<PostPublishedIntegrationEvent>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<NotifyOnPostPublishedConsumer> _log;

    public NotifyOnPostPublishedConsumer(
        INotificationDispatcher dispatcher,
        ApplicationDbContext db,
        ILogger<NotifyOnPostPublishedConsumer> log)
    {
        _dispatcher = dispatcher;
        _db = db;
        _log = log;
    }

    public async Task Consume(ConsumeContext<PostPublishedIntegrationEvent> context)
    {
        var m = context.Message;
        var post = await _db.Posts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == m.PostId, context.CancellationToken);
        if (post is null || !post.IsPublished) return;

        try
        {
            var n = await _dispatcher.NotifyFollowersOfNewPostAsync(post, context.CancellationToken);
            _log.LogInformation("Notified followers of post {PostId} count~={N}", m.PostId, n);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Follower notify failed PostId={Id}", m.PostId);
        }
    }
}

public sealed class NotifyOnCommentApprovedConsumer : IConsumer<CommentApprovedIntegrationEvent>
{
    private readonly INotificationService _notify;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<NotifyOnCommentApprovedConsumer> _log;

    public NotifyOnCommentApprovedConsumer(
        INotificationService notify,
        ApplicationDbContext db,
        ILogger<NotifyOnCommentApprovedConsumer> log)
    {
        _notify = notify;
        _db = db;
        _log = log;
    }

    public async Task Consume(ConsumeContext<CommentApprovedIntegrationEvent> context)
    {
        var m = context.Message;
        var comment = await _db.Comments.AsNoTracking().FirstOrDefaultAsync(c => c.Id == m.CommentId, context.CancellationToken);
        var post = await _db.Posts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == m.PostId, context.CancellationToken);
        if (comment is null || post is null) return;

        try
        {
            await _notify.NotifyNewCommentAsync(post, comment, context.CancellationToken);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Comment notify failed CommentId={Id}", m.CommentId);
        }
    }
}

public sealed class NotifyOnAuthorFollowedConsumer : IConsumer<AuthorFollowedIntegrationEvent>
{
    private readonly INotificationService _notify;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<NotifyOnAuthorFollowedConsumer> _log;

    public NotifyOnAuthorFollowedConsumer(
        INotificationService notify,
        ApplicationDbContext db,
        ILogger<NotifyOnAuthorFollowedConsumer> log)
    {
        _notify = notify;
        _db = db;
        _log = log;
    }

    public async Task Consume(ConsumeContext<AuthorFollowedIntegrationEvent> context)
    {
        var m = context.Message;
        var follower = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == m.FollowerUserId, context.CancellationToken);
        if (follower is null) return;

        try
        {
            await _notify.NotifyNewFollowerAsync(m.AuthorUserId, follower, context.CancellationToken);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Follower notify failed Author={A}", m.AuthorUserId);
        }
    }
}

/// <summary>Delivers outbound developer webhooks for notification.created events.</summary>
public sealed class WebhookDispatchConsumer : IConsumer<WebhookDispatchMessage>
{
    private readonly IWebhookDeliveryService _webhooks;

    public WebhookDispatchConsumer(IWebhookDeliveryService webhooks) => _webhooks = webhooks;

    public Task Consume(ConsumeContext<WebhookDispatchMessage> context) =>
        _webhooks.DispatchAsync(context.Message, context.CancellationToken);
}
