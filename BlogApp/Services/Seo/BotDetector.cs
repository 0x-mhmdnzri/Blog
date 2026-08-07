namespace BlogApp.Services.Seo;

/// <summary>Maps User-Agent strings to a stable bot family + kind.</summary>
public static class BotDetector
{
    public readonly record struct Match(string Family, string Kind);

    // Order matters: more specific tokens first.
    private static readonly (string Token, string Family, string Kind)[] Rules =
    [
        ("Googlebot-Image", "googlebot-image", "search"),
        ("Googlebot-News", "googlebot-news", "search"),
        ("Googlebot", "googlebot", "search"),
        ("Google-Extended", "google-extended", "ai"),
        ("AdsBot-Google", "adsbot-google", "search"),
        ("Mediapartners-Google", "mediapartners-google", "search"),
        ("bingbot", "bingbot", "search"),
        ("BingPreview", "bingpreview", "search"),
        ("DuckDuckBot", "duckduckbot", "search"),
        ("Slurp", "slurp", "search"),
        ("YandexBot", "yandex", "search"),
        ("Baiduspider", "baiduspider", "search"),
        ("Applebot-Extended", "applebot-extended", "ai"),
        ("Applebot", "applebot", "search"),
        ("GPTBot", "gptbot", "ai"),
        ("ChatGPT-User", "chatgpt-user", "ai"),
        ("OAI-SearchBot", "oai-searchbot", "ai"),
        ("ClaudeBot", "claudebot", "ai"),
        ("Claude-Web", "claude-web", "ai"),
        ("anthropic-ai", "anthropic-ai", "ai"),
        ("PerplexityBot", "perplexitybot", "ai"),
        ("Perplexity-User", "perplexity-user", "ai"),
        ("Bytespider", "bytespider", "ai"),
        ("CCBot", "ccbot", "ai"),
        ("FacebookBot", "facebookbot", "ai"),
        ("meta-externalagent", "meta-externalagent", "ai"),
        ("Amazonbot", "amazonbot", "ai"),
        ("cohere-ai", "cohere-ai", "ai"),
        ("Diffbot", "diffbot", "ai"),
        ("YouBot", "youbot", "ai"),
        ("Omgilibot", "omgilibot", "ai"),
        ("Omgili", "omgili", "ai"),
        ("ImagesiftBot", "imagesiftbot", "ai"),
        ("ia_archiver", "ia_archiver", "archive"),
        ("archive.org_bot", "archive-org", "archive"),
        ("SemrushBot", "semrushbot", "other"),
        ("AhrefsBot", "ahrefsbot", "other"),
        ("DotBot", "dotbot", "other"),
        ("PetalBot", "petalbot", "other"),
    ];

    public static bool TryMatch(string? userAgent, out Match match)
    {
        match = default;
        if (string.IsNullOrWhiteSpace(userAgent))
            return false;

        foreach (var (token, family, kind) in Rules)
        {
            if (userAgent.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                match = new Match(family, kind);
                return true;
            }
        }

        // Generic bot hint — still useful for waste analysis, tagged "other"
        if (userAgent.Contains("bot", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("crawl", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("spider", StringComparison.OrdinalIgnoreCase))
        {
            match = new Match("generic-bot", "other");
            return true;
        }

        return false;
    }
}
