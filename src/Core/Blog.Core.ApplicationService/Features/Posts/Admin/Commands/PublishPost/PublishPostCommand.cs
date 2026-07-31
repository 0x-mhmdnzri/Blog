namespace Blog.Core.ApplicationService.Features.Posts.Admin.Commands.PublishPost;

using Blog.Core.Contract.Primitives.Handlers;

public sealed class PublishPostCommand : ICommand
{
    public required string Slug { get; init; }
}
