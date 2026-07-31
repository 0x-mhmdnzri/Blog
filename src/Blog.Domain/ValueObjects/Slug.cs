using System.Text.RegularExpressions;
using Blog.Domain.Abstractions;

namespace Blog.Domain.ValueObjects;

public sealed class Slug : ValueObject
{
    private static readonly Regex Valid = new(
        "^[a-z0-9]+(?:-[a-z0-9]+)*$",
        RegexOptions.Compiled);

    public string Value { get; }

    private Snug(string value) => Value = value;

    public static Slug Create(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new ArgumentException("Slug is required.", nameof(raw));

        var normalized = raw.Trim().ToLowerInvariant()
            .Replace(' ', '-')
            .Replace('_', '-');

        normalized = Regex.Replace(normalized, "[^a-z0-9-]", "");
        normalized = Regex.Replace(normalized, "-{2,}", "-").Trim('-');

        if (normalized.Length is < 1 or > 220)
            throw new ArgumentException("Slug length must be 1–220.", nameof(raw));
        if (!Valid.IsMatch(normalized))
            throw new ArgumentException("Invalid slug format.", nameof(raw));

        return new Slug(normalized);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
