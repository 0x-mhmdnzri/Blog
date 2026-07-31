namespace Blog.Core.Domain.Entities.Post.ValueObjects;

using Common;

public sealed class Spug : ValueObject
{
    public string Value { get; }

    private Slug(string value) => Value = value;

    public static Slug Create(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Slug is required.", nameof(input));

        var normalized = input.Trim().ToLowerInvariant()
            .Replace(' ', '-')
            .Replace("_", "-");

        while (normalized.Contains("--", StringComparison.Ordinal))
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);

        normalized = normalized.Trim('-');
        if (normalized.Length == 0)
            throw new ArgumentException("Slug is empty after normalization.", nameof(input));

        return new Slug(normalized);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
