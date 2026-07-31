using Blog.Domain.Abstractions;
using Blog.Domain.Aggregates.Posts.Events;
using Blog.Domain.ValueObjects;

namespace Blog.Domain.Aggregates.Posts;

/// <summary>
/// Post aggregate root — owns publish lifecycle and raises domain events.
/// Maps to existing BlogApp.Models.Post persistence via application adapters.
/// </summary>
public sealed class PostAggregate : AggregateRoot
{
    public string Title { get; private set; } = string.Empty;
    public Slug Slug { get; private set; } = null!;
    public string? Summary { get; private set; }
    public string ContentMarkdown { get; private set; } = string.Empty;
    public string AuthorId { get; private set; } = string.Empty;
    public PostStatus Status { get; private set; } = PostStatus.Draft;
    public DateTime? PublishedAtUtc { get; private set; }
    public DateTime? ScheduledPublishAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public int ContentVersion { get; private set; }

    private PostAggregate() { }

    public static PostAggregate Create(
        string title,
        string slug,
        string contentMarkdown,
        string authorId,
        string? summary = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(authorId))
            throw new ArgumentException("Author is required.", nameof(authorId));

        var post = new PostAggregate
        {
            Id = Guid.NewGuid(),
            Title = title.Trim(),
            Slug = Slug.Create(slug),
            Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim(),
            ContentMarkdown = contentMarkdown ?? string.Empty,
            AuthorId = authorId,
            Status = PostStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            ContentVersion = 1
        };

        post.Raise(new PostCreatedDomainEvent(post.Id, post.Title, post.Slug.Value, post.AuthorId));
        return post;
    }

    public void UpdateContent(string title, string contentMarkdown, string? summary = null)
    {
        EnsureNotDeleted();
        Title = string.IsNullOrWhiteSpace(title) ? Title : title.Trim();
        ContentMarkdown = contentMarkdown ?? ContentMarkdown;
        if (summary is not null)
            Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
        ContentVersion++;
        Touch();
        Raise(new PostContentUpdatedDomainEvent(Id, Slug.Value, ContentVersion));
    }

    public void Publish(DateTime? atUtc = null)
    {
        EnsureNotDeleted();
        if (Status == PostStatus.Published)
            return;

        var when = atUtc ?? DateTime.UtcNow;
        if (when > DateTime.UtcNow.AddMinutes(1))
        {
            Status = PostStatus.Scheduled;
            ScheduledPublishAtUtc = when;
            Touch();
            return;
        }

        Status = PostStatus.Published;
        PublishedAtUtc = when;
        ScheduledPublishAtUtc = null;
        Touch();
        Raise(new PostPublishedDomainEvent(Id, Title, Slug.Value, AuthorId, when));
    }

    public void Unpublish()
    {
        EnsureNotDeleted();
        if (Status != PostStatus.Published && Status != PostStatus.Scheduled)
            return;

        Status = PostStatus.Draft;
        ScheduledPublishAtUtc = null;
        Touch();
        Raise(new PostUnpublishedDomainEvent(Id, Slug.Value));
    }

    public void SoftDelete()
    {
        if (Status == PostStatus.SoftDeleted)
            return;
        Status = PostStatus.SoftDeleted;
        Touch();
        Raise(new PostSoftDeletedDomainEvent(Id, Slug.Value));
    }

    public void Restore()
    {
        if (Status != PostStatus.SoftDeleted)
            return;
        Status = PostStatus.Draft;
        Touch();
        Raise(new PostRestoredDomainEvent(Id, Slug.Value));
    }

    private void EnsureNotDeleted()
    {
        if (Status == PostStatus.SoftDeleted)
            throw new InvalidOperationException("Post is soft-deleted.");
    }

    private void Touch() => UpdatedAtUtc = DateTime.UtcNow;
}
