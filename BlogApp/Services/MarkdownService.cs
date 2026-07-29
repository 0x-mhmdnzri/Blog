using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;

namespace BlogApp.Services;

public class MarkdownService
{
    private static readonly Regex VideoTokenRegex =
        new(@"\{\{\s*video\s*:\s*(\d+)\s*\}\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex HeadingRegex =
        new(@"^(#{1,6})\s+(.+)$", RegexOptions.Compiled | RegexOptions.Multiline);

    // Match <h1>…</h1> or <h2 id="x" class="y">…</h2> (Markdig AutoIdentifier)
    private static readonly Regex HeadingHtmlRegex =
        new(@"<h([1-6])(\s[^>]*)?>(.*?)</h\1>", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static readonly Regex ParagraphHtmlRegex =
        new(@"<p(\s[^>]*)?>(.*?)</p>", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static readonly Regex ListItemHtmlRegex =
        new(@"<li(\s[^>]*)?>(.*?)</li>", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static readonly Regex TableHtmlRegex =
        new(@"<table(\s[^>]*)?>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex CellHtmlRegex =
        new(@"<(t[dh])(\s[^>]*)?>(.*?)</\1>", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static readonly Regex PreHtmlRegex =
        new(@"<pre(\s[^>]*)?>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex CodeHtmlRegex =
        new(@"<code(\s[^>]*)?>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly MarkdownPipeline _pipeline;

    public MarkdownService()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseEmojiAndSmiley()
            .UseSoftlineBreakAsHardlineBreak()
            .UseAutoLinks()
            .DisableHtml()
            .Build();
    }

    public string RenderToHtml(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;
        var withVideos = VideoTokenRegex.Replace(markdown, match =>
        {
            var mediaId = match.Groups[1].Value;
            return $"\n\n[[VIDEO_EMBED_{mediaId}]]\n\n";
        });
        var html = Markdown.ToHtml(withVideos, _pipeline);

        // Code always LTR
        html = PreHtmlRegex.Replace(html, "<pre class=\"md-code-block\" dir=\"ltr\">");
        html = CodeHtmlRegex.Replace(html, "<code dir=\"ltr\">");

        // Paragraphs: explicit ltr/rtl from first strong character
        html = ParagraphHtmlRegex.Replace(html, m =>
        {
            var inner = m.Groups[2].Value;
            var plain = StripTags(inner);
            if (IsVideoPlaceholder(plain))
                return m.Value; // handled below
            var dir = DetectDir(plain);
            return $"<p dir=\"{dir}\" class=\"md-p\">{inner}</p>";
        });

        // List items (TOC + content lists)
        html = ListItemHtmlRegex.Replace(html, m =>
        {
            var inner = m.Groups[2].Value;
            var plain = StripTags(inner);
            var dir = DetectDir(plain);
            return $"<li dir=\"{dir}\" class=\"md-li\">{inner}</li>";
        });

        // Tables: neutral shell; cells by script
        html = TableHtmlRegex.Replace(html, "<table class=\"md-table\">");
        html = CellHtmlRegex.Replace(html, m =>
        {
            var tag = m.Groups[1].Value.ToLowerInvariant();
            var inner = m.Groups[3].Value;
            var dir = DetectDir(StripTags(inner));
            return $"<{tag} dir=\"{dir}\">{inner}</{tag}>";
        });

        // Video embeds
        html = Regex.Replace(
            html,
            @"<p dir=\"(?:ltr|rtl|auto)\" class=\"md-p\">\[\[VIDEO_EMBED_(\d+)\]\]</p>",
            m =>
            {
                var id = m.Groups[1].Value;
                return $"<div class=\"post-video-embed\"><video controls preload=\"metadata\" src=\"/media/{id}\"></video></div>";
            });
        html = Regex.Replace(
            html,
            @"<p>\[\[VIDEO_EMBED_(\d+)\]\]</p>",
            m =>
            {
                var id = m.Groups[1].Value;
                return $"<div class=\"post-video-embed\"><video controls preload=\"metadata\" src=\"/media/{id}\"></video></div>";
            });

        return html;
    }

    public string ToPlainText(string markdown, int maxLength = 200)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;
        var html = Markdown.ToPlainText(markdown, _pipeline);
        var plain = Regex.Replace(html, @"\s+", " ").Trim();
        return plain.Length <= maxLength ? plain : plain[..maxLength].TrimEnd() + "\u2026";
    }

    public int EstimateReadingTimeMinutes(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return 1;
        var plain = ToPlainText(markdown, int.MaxValue);
        var wordCount = plain.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        return Math.Max(1, (int)Math.Ceiling(wordCount / 200.0));
    }

    public string GenerateTableOfContents(string markdown, string cssClass = "post-toc")
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;
        var matches = HeadingRegex.Matches(markdown);
        if (matches.Count < 2) return string.Empty;

        var sb = new StringBuilder();
        // Shell is RTL (Persian title); each link gets its own dir.
        sb.Append($"<nav class=\"{cssClass}\" aria-label=\"فهرست مطالب\">");
        sb.Append("<p class=\"toc-title\" dir=\"rtl\">فهرست مطالب</p><ul class=\"toc-list\">");
        int prevLevel = 0;
        foreach (Match m in matches)
        {
            var level = m.Groups[1].Value.Length;
            var text = Regex.Replace(m.Groups[2].Value.Trim(), @"[*_`\[\]()#]", "").Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;
            var slug = SlugifyHeading(text);
            var dir = DetectDir(text);
            while (prevLevel > level) { sb.Append("</ul></li>"); prevLevel--; }
            if (prevLevel == level)
            {
                if (prevLevel > 0) sb.Append("</li>");
                sb.Append($"<li dir=\"{dir}\" class=\"toc-item\"><a href=\"#{slug}\" dir=\"{dir}\">{text}</a>");
            }
            else if (level > prevLevel)
            {
                for (int i = prevLevel; i < level - 1; i++) sb.Append("<ul><li>");
                if (prevLevel > 0) sb.Append("<ul>");
                sb.Append($"<li dir=\"{dir}\" class=\"toc-item\"><a href=\"#{slug}\" dir=\"{dir}\">{text}</a>");
            }
            prevLevel = level;
        }
        while (prevLevel > 0) { sb.Append("</li></ul>"); prevLevel--; }
        sb.Append("</ul></nav>");
        return sb.ToString();
    }

    public string RenderToHtmlWithToc(string markdown, bool includeToc = true)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;
        var html = RenderToHtml(markdown);

        // Headings — keep existing attrs (id from Markdig), force dir from text
        html = HeadingHtmlRegex.Replace(html, m =>
        {
            var level = m.Groups[1].Value;
            var attrs = m.Groups[2].Value ?? "";
            var inner = m.Groups[3].Value;
            var plain = StripTags(inner).Trim();
            var dir = DetectDir(plain);
            var id = SlugifyHeading(plain);
            // Drop any existing id; we set a stable one
            attrs = Regex.Replace(attrs, @"\s*id\s*=\s*[""'][^""']*[""']", "", RegexOptions.IgnoreCase);
            attrs = Regex.Replace(attrs, @"\s*dir\s*=\s*[""'][^""']*[""']", "", RegexOptions.IgnoreCase);
            return $"<h{level} id=\"{id}\" dir=\"{dir}\" class=\"md-heading\"{attrs}>{inner}</h{level}>";
        });

        if (includeToc)
        {
            var toc = GenerateTableOfContents(markdown);
            if (!string.IsNullOrEmpty(toc)) html = toc + html;
        }
        return html;
    }

    /// <summary>
    /// First strong bidirectional character wins. Persian/Arabic → rtl, Latin → ltr.
    /// </summary>
    public static string DetectDir(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "auto";
        foreach (var rune in text.EnumerateRunes())
        {
            var c = rune.Value;
            if (Rune.IsWhiteSpace(rune) || Rune.IsDigit(rune) || IsNeutral(c))
                continue;

            // Arabic, Persian, presentation forms
            if (c is (>= 0x0600 and <= 0x06FF)
                or (>= 0x0750 and <= 0x077F)
                or (>= 0x08A0 and <= 0x08FF)
                or (>= 0xFB50 and <= 0xFDFF)
                or (>= 0xFE70 and <= 0xFEFF))
                return "rtl";

            if (Rune.IsLetter(rune))
                return "ltr";
        }
        return "auto";
    }

    private static bool IsNeutral(int c) =>
        char.GetUnicodeCategory((char)Math.Clamp(c, 0, 0xFFFF)) is
            UnicodeCategory.OtherPunctuation
            or UnicodeCategory.DashPunctuation
            or UnicodeCategory.ConnectorPunctuation
            or UnicodeCategory.OpenPunctuation
            or UnicodeCategory.ClosePunctuation
            or UnicodeCategory.InitialQuotePunctuation
            or UnicodeCategory.FinalQuotePunctuation
            or UnicodeCategory.MathSymbol
            or UnicodeCategory.CurrencySymbol
            or UnicodeCategory.ModifierSymbol
            or UnicodeCategory.OtherSymbol;

    private static string StripTags(string html) =>
        Regex.Replace(html, "<.*?>", string.Empty);

    private static bool IsVideoPlaceholder(string plain) =>
        plain.StartsWith("[[VIDEO_EMBED_", StringComparison.Ordinal);

    private static string SlugifyHeading(string text)
    {
        var s = text.ToLowerInvariant().Trim();
        s = Regex.Replace(s, @"\s+", "-");
        s = Regex.Replace(s, "[^\\w\u0600-\u06FF\\-]", "");
        return s.Length > 80 ? s[..80] : s;
    }
}
