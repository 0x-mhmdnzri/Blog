namespace Blog.Core.Domain.Entities.Post;

using Common;
using DomainEvents;
using ValueObjects;

public enum PostLifecycleStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2
}

public sealed class Post : AggregateRoot
{
    public string Title { get; private set; } = string.Empty;
    public Slug Slug { get; private set; } = null!;
    public string BodyMarkdown { get; private set; } = string.Empty;
    public string AuthorId { get; private set; } = string.Empty;
    public PostLifecycleStatus Status { get; private set; } = PostLifecycleStatus.Draft;
    public DateTime? PublishedAtUtc { get; private set; }

    private Post() { }

    public static Post Create(string title, Slug slug, string bodyMarkdown, string authorId)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(authorId))
            throw new ArgumentException("Author is required.", nameof(authorId));

        var post = new Post
        {
            Title = title.Trim(),
            Slug = slug,
            BodyMarkdown = bodyMarkdown ?? string.Empty,
            AuthorId = authorId,
            Status = PostLifecycleStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };
        post.RaiseDomainEvent(new PostCreatedEvent(post.Slug.Value, post.AuthorId));
        return post;
    }

    public void Publish()
    {
        if (Status == PostLifecycleStatus.Published) return;
        Status = PostLifecycleStatus.Published;
        PublishedAtUtc = DateTime.UtcNow;
        RaiseDomainEvent(new PostPublishedEvent(Slug.Value, AuthorId, PublishedAtUtc.Value));
    }

    public void UpdateContent(string title, string bodyMarkdown)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        Title = title.Trim();
        BodyMarkdown = bodyMarkdown ?? string.Empty;
        RaiseDomainEvent(new PostUpdatedEvent(Slug.Value));
    }
}

public sealed record PostCreatedEvent(string Slug, string AuthorId) : DomainEventBase;
public sealed record PostPublishedEvent(string Slug, string AuthorId, DateTime PublishedAtUtc) : DomainEventBase;
public sealed record PostUpdatedEvent(string Slug) : DomainEventBase;
