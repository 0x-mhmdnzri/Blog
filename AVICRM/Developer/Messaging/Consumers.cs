using AVICRM.Developer.Domain;
using AVICRM.Developer.Observability;
using AVICRM.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AVICRM.Developer.Messaging;

/// <summary>Reacts to post.published — metrics, IndexNow ping, structured log.</summary>
public sealed class PostPublishedConsumer : IConsumer<PostPublishedIntegrationEvent>
{
    private readonly ILogger<PostPublishedConsumer> _log;
    private readonly IIndexNowService _indexNow;

    public PostPublishedConsumer(ILogger<PostPublishedConsumer> log, IIndexNowService indexNow)
    {
        _log = log;
        _indexNow = indexNow;
    }

    public async Task Consume(ConsumeContext<PostPublishedIntegrationEvent> context)
    {
        var m = context.Message;
        BlogMetrics.PostsPublished.Add(1);
        _log.LogInformation(
            "EDD PostPublished PostId={PostId} Slug={Slug} Author={Author} At={At:o}",
            m.PostId, m.Slug, m.AuthorId, m.PublishedAtUtc);

        try
        {
            // Language not on integration event — IndexNow builds URL with default path helpers
            await _indexNow.NotifyPostAsync(m.PostId, m.Slug, languageCode: null, context.CancellationToken);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "IndexNow ping failed PostId={Id}", m.PostId);
        }
    }
}

public sealed class PostCreatedConsumer : IConsumer<PostCreatedIntegrationEvent>
{
    private readonly ILogger<PostCreatedConsumer> _log;

    public PostCreatedConsumer(ILogger<PostCreatedConsumer> log) => _log = log;

    public Task Consume(ConsumeContext<PostCreatedIntegrationEvent> context)
    {
        var m = context.Message;
        BlogMetrics.PostsCreated.Add(1);
        _log.LogInformation("EDD PostCreated PostId={PostId} Slug={Slug}", m.PostId, m.Slug);
        return Task.CompletedTask;
    }
}

public sealed class CommentApprovedConsumer : IConsumer<CommentApprovedIntegrationEvent>
{
    private readonly ILogger<CommentApprovedConsumer> _log;

    public CommentApprovedConsumer(ILogger<CommentApprovedConsumer> log) => _log = log;

    public Task Consume(ConsumeContext<CommentApprovedIntegrationEvent> context)
    {
        var m = context.Message;
        BlogMetrics.CommentsApproved.Add(1);
        _log.LogInformation("EDD CommentApproved CommentId={Id} Post={Post}", m.CommentId, m.PostSlug);
        return Task.CompletedTask;
    }
}

public sealed class AuthorFollowedConsumer : IConsumer<AuthorFollowedIntegrationEvent>
{
    private readonly ILogger<AuthorFollowedConsumer> _log;

    public AuthorFollowedConsumer(ILogger<AuthorFollowedConsumer> log) => _log = log;

    public Task Consume(ConsumeContext<AuthorFollowedIntegrationEvent> context)
    {
        var m = context.Message;
        BlogMetrics.AuthorFollows.Add(1);
        _log.LogInformation("EDD AuthorFollowed Follower={F} Author={A}", m.FollowerUserId, m.AuthorUserId);
        return Task.CompletedTask;
    }
}
