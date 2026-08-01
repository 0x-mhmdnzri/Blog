namespace AVICRM.Developer.Domain;

/// <summary>Domain event raised by aggregates (in-process + MassTransit bridge).</summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredOnUtc { get; }
    string EventName { get; }
}

public abstract record DomainEventBase : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
    public abstract string EventName { get; }
}

/// <summary>Integration message for MassTransit (serializable, cross-boundary).</summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredOnUtc { get; }
}

public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}

// ── Post domain events ──────────────────────────────────────────────────────

public sealed record PostCreatedDomainEvent(
    int PostId,
    string Title,
    string Slug,
    string AuthorId) : DomainEventBase
{
    public override string EventName => "post.created";
}

public sealed record PostPublishedDomainEvent(
    int PostId,
    string Title,
    string Slug,
    string AuthorId,
    DateTime PublishedAtUtc) : DomainEventBase
{
    public override string EventName => "post.published";
}

public sealed record PostUnpublishedDomainEvent(
    int PostId,
    string Slug) : DomainEventBase
{
    public override string EventName => "post.unpublished";
}

public sealed record PostSoftDeletedDomainEvent(
    int PostId,
    string Slug) : DomainEventBase
{
    public override string EventName => "post.soft_deleted";
}

public sealed record CommentApprovedDomainEvent(
    int CommentId,
    int PostId,
    string PostSlug) : DomainEventBase
{
    public override string EventName => "comment.approved";
}

public sealed record AuthorFollowedDomainEvent(
    string FollowerUserId,
    string AuthorUserId) : DomainEventBase
{
    public override string EventName => "author.followed";
}

// ── Integration events (MassTransit contracts) ──────────────────────────────

public sealed record PostPublishedIntegrationEvent : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
    public int PostId { get; init; }
    public string Title { get; init; } = "";
    public string Slug { get; init; } = "";
    public string AuthorId { get; init; } = "";
    public DateTime PublishedAtUtc { get; init; }
}

public sealed record PostCreatedIntegrationEvent : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
    public int PostId { get; init; }
    public string Title { get; init; } = "";
    public string Slug { get; init; } = "";
    public string AuthorId { get; init; } = "";
}

public sealed record CommentApprovedIntegrationEvent : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
    public int CommentId { get; init; }
    public int PostId { get; init; }
    public string PostSlug { get; init; } = "";
}

public sealed record AuthorFollowedIntegrationEvent : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
    public string FollowerUserId { get; init; } = "";
    public string AuthorUserId { get; init; } = "";
}
