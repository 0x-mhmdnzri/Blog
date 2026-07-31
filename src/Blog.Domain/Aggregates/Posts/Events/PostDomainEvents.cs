using Blog.Domain.Abstractions;

namespace Blog.Domain.Aggregates.Posts.Events;

public sealed record PostCreatedDomainEvent(
    Guid PostId,
    string Title,
    string Slug,
    string AuthorId) : DomainEventBase
{
    public override string EventName => "post.created";
}

public sealed record PostPublishedDomainEvent(
    Guid PostId,
    string Title,
    string Slug,
    string AuthorId,
    DateTime PublishedAtUtc) : DomainEventBase
{
    public override string EventName => "post.published";
}

public sealed record PostUnpublishedDomainEvent(
    Guid PostId,
    string Slug) : DomainEventBase
{
    public override string EventName => "post.unpublished";
}

public sealed record PostSoftDeletedDomainEvent(
    Guid PostId,
    string Slug) : DomainEventBase
{
    public override string EventName => "post.soft_deleted";
}

public sealed record PostRestoredDomainEvent(
    Guid PostId,
    string Slug) : DomainEventBase
{
    public override string EventName => "post.restored";
}

public sealed record PostContentUpdatedDomainEvent(
    Guid PostId,
    string Slug,
    int RevisionHint) : DomainEventBase
{
    public override string EventName => "post.content_updated";
}
