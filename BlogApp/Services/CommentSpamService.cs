using System.Text.RegularExpressions;
using BlogApp.Models;
using Microsoft.Extensions.Options;

namespace BlogApp.Services;

public class CommentSpamOptions
{
    public const string Section = "Comments";

    public bool SpamDetectionEnabled { get; set; } = true;
    /// <summary>Score ≥ this marks status Spam (hidden from public, visible in admin).</summary>
    public int SpamThreshold { get; set; } = 60;
    public int MaxLinks { get; set; } = 2;
    public int MinBodyLength { get; set; } = 2;
    public int MaxBodyLength { get; set; } = 2000;
    public int EditWindowMinutes { get; set; } = 15;
    public bool GuestCommentsEnabled { get; set; } = true;
    public bool AutoApproveAuthenticated { get; set; } = false;

    /// <summary>Max nesting depth for Twitter-style reply threads (1 = only reply to root).</summary>
    public int MaxReplyDepth { get; set; } = 5;

    /// <summary>Comma-separated blocked substrings (case-insensitive).</summary>
    public string BlockedKeywords { get; set; } =
        "viagra,cialis,casino,crypto-giveaway,click here now,free money,work from home $$$";
}

public interface ICommentSpamService
{
    CommentSpamResult Evaluate(string authorName, string body, string? authorEmail, bool isGuest);
}

public sealed class CommentSpamResult
{
    public int Score { get; init; }
    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();
    public bool IsSpam { get; init; }
}

/// <summary>Lightweight rule-based spam filter (no external ML dependency).</summary>
public sealed class CommentSpamService : ICommentSpamService
{
    private static readonly Regex UrlRegex = new(
        @"(https?://|www\.)\S+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RepeatedChars = new(
        @"(.)\1{6,}",
        RegexOptions.Compiled);

    private static readonly Regex MostlyNonLetter = new(
        @"^[^\p{L}\s]{8,}$",
        RegexOptions.Compiled);

    private readonly CommentSpamOptions _opt;
    private readonly string[] _blocked;

    public CommentSpamService(IOptions<CommentSpamOptions> opt)
    {
        _opt = opt.Value;
        _blocked = (_opt.BlockedKeywords ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 0)
            .ToArray();
    }

    public CommentSpamResult Evaluate(string authorName, string body, string? authorEmail, bool isGuest)
    {
        if (!_opt.SpamDetectionEnabled)
            return new CommentSpamResult { Score = 0, IsSpam = false };

        var score = 0;
        var reasons = new List<string>();

        var name = (authorName ?? "").Trim();
        var text = (body ?? "").Trim();

        if (text.Length < _opt.MinBodyLength)
        {
            score += 20;
            reasons.Add("too_short");
        }

        if (text.Length > _opt.MaxBodyLength)
        {
            score += 30;
            reasons.Add("too_long");
        }

        var links = UrlRegex.Matches(text).Count;
        if (links > _opt.MaxLinks)
        {
            score += 25 + Math.Min(40, (links - _opt.MaxLinks) * 15);
            reasons.Add($"too_many_links:{links}");
        }

        if (links > 0 && text.Length < 40)
        {
            score += 20;
            reasons.Add("link_heavy_short");
        }

        if (RepeatedChars.IsMatch(text))
        {
            score += 15;
            reasons.Add("repeated_chars");
        }

        if (MostlyNonLetter.IsMatch(text))
        {
            score += 25;
            reasons.Add("mostly_symbols");
        }

        var lower = text.ToLowerInvariant();
        foreach (var kw in _blocked)
        {
            if (lower.Contains(kw.ToLowerInvariant()))
            {
                score += 40;
                reasons.Add($"blocked:{kw}");
            }
        }

        if (isGuest && string.IsNullOrWhiteSpace(authorEmail) && links > 0)
        {
            score += 10;
            reasons.Add("guest_link_no_email");
        }

        if (name.Length < 2)
        {
            score += 15;
            reasons.Add("bad_name");
        }

        score = Math.Clamp(score, 0, 100);
        return new CommentSpamResult
        {
            Score = score,
            Reasons = reasons,
            IsSpam = score >= _opt.SpamThreshold
        };
    }
}
