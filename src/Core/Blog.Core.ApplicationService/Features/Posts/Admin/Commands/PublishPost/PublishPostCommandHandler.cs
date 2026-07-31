namespace Blog.Core.ApplicationService.Features.Posts.Admin.Commands.PublishPost;

using Blog.Core.Contract.Features.Posts;
using Blog.Core.Contract.Primitives.Handlers;
using Blog.Core.Contract.Primitives.Messaging;
using Microsoft.Extensions.Logging;

public sealed class PublishPostCommandHandler : ICommandHandler<PublishPostCommand>
{
    private readonly IPostCommandRepository _posts;
    private readonly IEventPublisher _events;
    private readonly ILogger<PublishPostCommandHandler> _logger;

    public PublishPostCommandHandler(
        IPostCommandRepository posts,
        IEventPublisher events,
        ILogger<PublishPostCommandHandler> logger)
    {
        _posts = posts;
        _events = events;
        _logger = logger;
    }

    public async Task Handle(PublishPostCommand command, CancellationToken cancellationToken = default)
    {
        var post = await _posts.GetBySlugAsync(command.Slug, cancellationToken)
            ?? throw new InvalidOperationException($"Post '{command.Slug}' not found.");

        post.Publish();
        await _events.PublishAsync(post.DomainEvents, cancellationToken);
        post.ClearDomainEvents();
        await _posts.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Published post {Slug}", command.Slug);
    }
}
