using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace AVICRM.Services;

public class AiOptions
{
    /// <summary>When set, calls OpenAI-compatible Chat Completions (OpenAI, Azure, local Ollama, etc.).</summary>
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gpt-4o-mini";
    public int TimeoutSeconds { get; set; } = 45;
    public bool Enabled => !string.IsNullOrWhiteSpace(Endpoint);
}

/// <summary>
/// AI-assisted content helpers. Uses local heuristics always;
/// when Ai:Endpoint is configured, prefers LLM for summarize / grammar / assist.
/// </summary>
public class AiContentService
{
    private readonly MarkdownService _markdown;
    private readonly AiOptions _opt;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<AiContentService> _log;

    public AiContentService(
        MarkdownService markdown,
        IOptions<AiOptions> opt,
        IHttpClientFactory http,
        ILogger<AiContentService> log)
    {
        _markdown = markdown;
        _opt = opt.Value;
        _http = http;
        _log = log;
    }

    public async Task<string> SummarizeAsync(string markdown, int maxLength = 160, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;

        if (_opt.Enabled)
        {
            var llm = await ChatAsync(
                system: "You summarize blog posts. Reply with a single plain summary under 160 characters. No quotes or labels.",
                user: markdown.Length > 6000 ? markdown[..6000] : markdown,
                ct);
            if (!string.IsNullOrWhiteSpace(llm))
                return llm.Trim().Length <= maxLength ? llm.Trim() : llm.Trim()[..maxLength].TrimEnd() + "…";
        }

        return SummarizeLocal(markdown, maxLength);
    }

    public string Summarize(string markdown, int maxLength = 160) =>
        SummarizeAsync(markdown, maxLength).GetAwaiter().GetResult();

    public async Task<IReadOnlyList<string>> CheckGrammarAndStyleAsync(string markdown, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return Array.Empty<string>();

        if (_opt.Enabled)
        {
            var llm = await ChatAsync(
                system: "You are a writing coach. List up to 8 short, concrete style/grammar suggestions for the blog draft. One suggestion per line. Match draft language.",
                user: markdown.Length > 8000 ? markdown[..8000] : markdown,
                ct);
            if (!string.IsNullOrWhiteSpace(llm))
            {
                var lines = llm.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(l => Regex.Replace(l, @"^[\d\-\*\.\)\s]+", "").Trim())
                    .Where(l => l.Length > 2)
                    .Take(10)
                    .ToList();
                if (lines.Count > 0) return lines;
            }
        }

        return CheckGrammarLocal(markdown);
    }

    public IReadOnlyList<string> CheckGrammarAndStyle(string markdown) =>
        CheckGrammarAndStyleAsync(markdown).GetAwaiter().GetResult();

    public async Task<(string SuggestedTitle, IReadOnlyList<string> SuggestedTags)> AssistContentGenerationAsync(
        string markdown, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return ("Suggested title", Array.Empty<string>());

        if (_opt.Enabled)
        {
            var llm = await ChatAsync(
                system: "Reply in JSON only: {\"title\":\"...\",\"tags\":[\"a\",\"b\"]}. Title under 80 chars. Up to 6 short tags. Match draft language.",
                user: markdown.Length > 6000 ? markdown[..6000] : markdown,
                ct);
            if (!string.IsNullOrWhiteSpace(llm))
            {
                try
                {
                    var json = ExtractJson(llm);
                    using var doc = JsonDocument.Parse(json);
                    var title = doc.RootElement.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                    var tags = new List<string>();
                    if (doc.RootElement.TryGetProperty("tags", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var x in arr.EnumerateArray())
                        {
                            var s = x.GetString();
                            if (!string.IsNullOrWhiteSpace(s)) tags.Add(s.Trim());
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(title))
                        return (title.Trim(), tags);
                }
                catch (Exception ex)
                {
                    _log.LogDebug(ex, "AI assist JSON parse failed");
                }
            }
        }

        return AssistLocal(markdown);
    }

    public (string SuggestedTitle, IReadOnlyList<string> SuggestedTags) AssistContentGeneration(string markdown) =>
        AssistContentGenerationAsync(markdown).GetAwaiter().GetResult();

    private async Task<string?> ChatAsync(string system, string user, CancellationToken ct)
    {
        try
        {
            var client = _http.CreateClient("AiContent");
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(_opt.TimeoutSeconds, 5, 120));
            if (!string.IsNullOrWhiteSpace(_opt.ApiKey))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _opt.ApiKey);

            var payload = new
            {
                model = _opt.Model,
                temperature = 0.3,
                messages = new[]
                {
                    new { role = "system", content = system },
                    new { role = "user", content = user }
                }
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, _opt.Endpoint);
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var res = await client.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                _log.LogWarning("AI endpoint {Status}", (int)res.StatusCode);
                return null;
            }

            await using var stream = await res.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "AI ChatAsync failed — using local heuristics");
            return null;
        }
    }

    private static string ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start) return text[start..(end + 1)];
        return text;
    }

    private string SummarizeLocal(string markdown, int maxLength)
    {
        var plain = _markdown.ToPlainText(markdown, maxLength * 2);
        var cut = plain.Length <= maxLength ? plain : plain[..maxLength];
        var lastPeriod = cut.LastIndexOfAny(new[] { '.', '!', '?', '\u06D4', '\u061F' });
        if (lastPeriod > maxLength / 2)
            cut = cut[..(lastPeriod + 1)];
        return cut.Trim() + (plain.Length > cut.Length ? "\u2026" : "");
    }

    private IReadOnlyList<string> CheckGrammarLocal(string markdown)
    {
        var hints = new List<string>();
        var plain = _markdown.ToPlainText(markdown, int.MaxValue);
        var sentences = Regex.Split(plain, @"(?<=[.!?\u06D4\u061F])\s+");

        foreach (var s in sentences)
        {
            if (s.Length > 280)
                hints.Add($"Long sentence ({s.Length} chars): «{Truncate(s, 60)}…»");
        }

        var words = plain.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 1; i < words.Length; i++)
        {
            if (string.Equals(words[i], words[i - 1], StringComparison.OrdinalIgnoreCase) && words[i].Length > 2)
            {
                hints.Add($"Repeated word: «{words[i]}»");
                break;
            }
        }

        var excl = Regex.Matches(plain, @"!|\uFF01").Count;
        if (excl > 5)
            hints.Add($"Many exclamation marks ({excl}).");

        if (hints.Count == 0)
            hints.Add("No local issues found. (Heuristic — set Ai:Endpoint for LLM.)");

        return hints;
    }

    private (string, IReadOnlyList<string>) AssistLocal(string markdown)
    {
        var plain = _markdown.ToPlainText(markdown, 300);
        var firstSentence = plain.Split(new[] { '.', '!', '?', '\u06D4', '\u061F' }, 2)[0].Trim();
        var title = firstSentence.Length > 80 ? firstSentence[..80].Trim() + "…" : firstSentence;
        if (string.IsNullOrWhiteSpace(title)) title = "Suggested title";

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
