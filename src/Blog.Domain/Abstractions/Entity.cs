namespace Blog.Domain.Abstractions;

/// <summary>Base entity with identity equality.</summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public override bool Equals(object? obj)
    {
        if (obj is not Entity other) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;
        return Id == other.Id;
    }

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(Entity? a, Entity? b) =>
        a is null ? b is null : a.Equals(b);

    public static bool operator !=(Entity? a, Entity? b) => !(a == b);
}
