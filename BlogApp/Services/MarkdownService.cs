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

        html = Regex.Replace(html, @"<pre>", "<pre class=\"md-code-block\" dir=\"ltr\">");
        html = Regex.Replace(html, @"<code>", "<code dir=\"ltr\">");
        html = Regex.Replace(html, @"<table>", "<table class=\"md-table\" dir=\"rtl\">");
        html = Regex.Replace(html, @"<td>", "<td dir=\"auto\">");
        html = Regex.Replace(html, @"<th>", "<th dir=\"auto\">");
        html = Regex.Replace(html, @"<p>", "<p dir=\"auto\">");

        html = Regex.Replace(
            html,
            @"<p dir=""auto"">\[\[VIDEO_EMBED_(\d+)\]\]</p>",
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
        sb.Append($"<nav class=\"{cssClass}\" aria-label=\"فهرست مطالب\" dir=\"rtl\">");
        sb.Append("<p class=\"toc-title\">فهرست مطالب</p><ul>");
        int prevLevel = 0;
        foreach (Match m in matches)
        {
            var level = m.Groups[1].Value.Length;
            var text = Regex.Replace(m.Groups[2].Value.Trim(), @"[*_`\[\]()#]", "").Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;
            var slug = SlugifyHeading(text);
            while (prevLevel > level) { sb.Append("</ul></li>"); prevLevel--; }
            if (prevLevel == level)
            {
                if (prevLevel > 0) sb.Append("</li>");
                sb.Append($"<li><a href=\"#{slug}\" dir=\"auto\">{text}</a>");
            }
            else if (level > prevLevel)
            {
                for (int i = prevLevel; i < level - 1; i++) sb.Append("<ul><li>");
                if (prevLevel > 0) sb.Append("<ul>");
                sb.Append($"<li><a href=\"#{slug}\" dir=\"auto\">{text}</a>");
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
        html = Regex.Replace(html, @"<h([1-6])>(.*?)</h\1>", m =>
        {
            var level = m.Groups[1].Value;
            var inner = m.Groups[2].Value;
            var plain = Regex.Replace(inner, "<.*?>", "").Trim();
            return $"<h{level} id=\"{SlugifyHeading(plain)}\" dir=\"auto\">{inner}</h{level}>";
        }, RegexOptions.Singleline);
        if (includeToc)
        {
            var toc = GenerateTableOfContents(markdown);
            if (!string.IsNullOrEmpty(toc)) html = toc + html;
        }
        return html;
    }

    private static string SlugifyHeading(string text)
    {
        var s = text.ToLowerInvariant().Trim();
        s = Regex.Replace(s, @"\s+", "-");
        // Pattern seen by Regex: [^\w\u0600-\u06FF\-]
        // C# non-verbatim needs \\w and \\- ; \u0600 is a real Unicode escape.
        s = Regex.Replace(s, "[^\\w\u0600-\u06FF\\-]", "");
        return s.Length > 80 ? s[..80] : s;
    }
}
