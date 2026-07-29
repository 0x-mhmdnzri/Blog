using System.Text;
using System.Text.RegularExpressions;

namespace BlogApp.Services;

/// <summary>
/// Lightweight AI-assisted content helpers (local heuristics).
/// Can later be wired to a real LLM API via HttpClient + config.
/// </summary>
public class AiContentService
{
    private readonly MarkdownService _markdown;

    public AiContentService(MarkdownService markdown)
    {
        _markdown = markdown;
    }

    public string Summarize(string markdown, int maxLength = 160)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;
        var plain = _markdown.ToPlainText(markdown, maxLength * 2);
        var cut = plain.Length <= maxLength ? plain : plain[..maxLength];
        var lastPeriod = cut.LastIndexOfAny(new[] { '.', '!', '?', '\u06D4', '\u061F' });
        if (lastPeriod > maxLength / 2)
            cut = cut[..(lastPeriod + 1)];
        return cut.Trim() + (plain.Length > cut.Length ? "\u2026" : "");
    }

    public IReadOnlyList<string> CheckGrammarAndStyle(string markdown)
    {
        var hints = new List<string>();
        if (string.IsNullOrWhiteSpace(markdown)) return hints;

        var plain = _markdown.ToPlainText(markdown, int.MaxValue);
        var sentences = Regex.Split(plain, @"(?<=[.!?\u06D4\u061F])\s+");

        foreach (var s in sentences)
        {
            if (s.Length > 280)
                hints.Add($"\u062C\u0645\u0644\u0647 \u0628\u0633\u06CC\u0627\u0631 \u0637\u0648\u0644\u0627\u0646\u06CC ({s.Length} \u0646\u0648\u06CC\u0633\u0647): \u00AB{Truncate(s, 60)}\u2026\u00BB");
        }

        var words = plain.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 1; i < words.Length; i++)
        {
            if (string.Equals(words[i], words[i - 1], StringComparison.OrdinalIgnoreCase) && words[i].Length > 2)
            {
                hints.Add($"\u06A9\u0644\u0645\u0647 \u062A\u06A9\u0631\u0627\u0631\u06CC \u067E\u0634\u062A\u200C\u0633\u0631\u0647\u0645: \u00AB{words[i]}\u00BB");
                break;
            }
        }

        var excl = Regex.Matches(plain, @"!|\uFF01").Count;
        if (excl > 5)
            hints.Add($"\u062A\u0639\u062F\u0627\u062F \u0639\u0644\u0627\u0645\u062A \u062A\u0639\u062C\u0628 \u0632\u06CC\u0627\u062F \u0627\u0633\u062A ({excl}).");

        if (hints.Count == 0)
            hints.Add("\u0646\u06A9\u062A\u0647\u200C\u0627\u06CC \u06CC\u0627\u0641\u062A \u0646\u0634\u062F. (\u0628\u0631\u0631\u0633\u06CC \u0645\u062D\u0644\u06CC \u0633\u0627\u062F\u0647)");

        return hints;
    }

    public (string SuggestedTitle, IReadOnlyList<string> SuggestedTags) AssistContentGeneration(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return ("\u0639\u0646\u0648\u0627\u0646 \u067E\u06CC\u0634\u0646\u0647\u0627\u062F\u06CC", Array.Empty<string>());

        var plain = _markdown.ToPlainText(markdown, 300);
        var firstSentence = plain.Split(new[] { '.', '!', '?', '\u06D4', '\u061F' }, 2)[0].Trim();
        var title = firstSentence.Length > 80 ? firstSentence[..80].Trim() + "\u2026" : firstSentence;
        if (string.IsNullOrWhiteSpace(title)) title = "\u0639\u0646\u0648\u0627\u0646 \u067E\u06CC\u0634\u0646\u0647\u0627\u062F\u06CC";

        var words = Regex.Matches(plain.ToLowerInvariant(), @"[\w\u0600-\u06FF]{4,}")
            .Select(m => m.Value)
            .Where(w => w.Length >= 4)
            .GroupBy(w => w)
            .OrderByDescending(g => g.Count())
            .Take(6)
            .Select(g => g.Key)
            .ToList();

        return (title, words);
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max];
}
