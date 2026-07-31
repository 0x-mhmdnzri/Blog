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

    private static readonly Regex ImgHtmlRegex =
        new(@"<img\s+([^>]*?)\s*/?>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex VideoEmbedDirRegex =
        new("<p dir=\"(?:ltr|rtl|auto)\" class=\"md-p\">\\[\\[VIDEO_EMBED_(\\d+)\\]\\]</p>", RegexOptions.Compiled);

    private static readonly Regex VideoEmbedPlainRegex =
        new("<p>\\[\\[VIDEO_EMBED_(\\d+)\\]\\]</p>", RegexOptions.Compiled);

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

        html = PreHtmlRegex.Replace(html, "<pre class=\"md-code-block\" dir=\"ltr\">");
        html = CodeHtmlRegex.Replace(html, "<code dir=\"ltr\">");

        html = ParagraphHtmlRegex.Replace(html, m =>
        {
            var inner = m.Groups[2].Value;
            var plain = StripTags(inner);
            if (IsVideoPlaceholder(plain))
                return m.Value;
            var dir = DetectDir(plain);
            return $"<p dir=\"{dir}\" class=\"md-p\">{inner}</p>";
        });

        html = ListItemHtmlRegex.Replace(html, m =>
        {
            var inner = m.Groups[2].Value;
            var plain = StripTags(inner);
            var dir = DetectDir(plain);
            return $"<li dir=\"{dir}\" class=\"md-li\">{inner}</li>";
        });

        html = TableHtmlRegex.Replace(html, "<table class=\"md-table\">");
        html = CellHtmlRegex.Replace(html, m =>
        {
            var tag = m.Groups[1].Value.ToLowerInvariant();
            var inner = m.Groups[3].Value;
            var dir = DetectDir(StripTags(inner));
            return $"<{tag} dir=\"{dir}\">{inner}</{tag}>";
        });

        html = ImgHtmlRegex.Replace(html, m =>
        {
            var attrs = m.Groups[1].Value;
            if (attrs.Contains("media-blur", StringComparison.OrdinalIgnoreCase))
                return m.Value;
            attrs = Regex.Replace(attrs, "\\s*loading\\s*=\\s*[\"'][^\"']*[\"']", "", RegexOptions.IgnoreCase);
            attrs = Regex.Replace(attrs, "\\s*decoding\\s*=\\s*[\"'][^\"']*[\"']", "", RegexOptions.IgnoreCase);
            if (Regex.IsMatch(attrs, "\\bclass\\s*=", RegexOptions.IgnoreCase))
                attrs = Regex.Replace(attrs, "class\\s*=\\s*[\"']([^\"']*)[\"']", "class=\"$1 media-blur\"", RegexOptions.IgnoreCase);
            else
                attrs += " class=\"media-blur\"";
            return $"<span class=\"media-blur-wrap\"><img {attrs} loading=\"lazy\" decoding=\"async\" /></span>";
        });

        html = VideoEmbedDirRegex.Replace(html, m => VideoEmbedHtml(m.Groups[1].Value));
        html = VideoEmbedPlainRegex.Replace(html, m => VideoEmbedHtml(m.Groups[1].Value));

        return html;
    }

    private static string VideoEmbedHtml(string id) =>
        $"<div class=\"post-video-embed media-blur-wrap\"><video class=\"media-blur\" controls preload=\"metadata\" playsinline src=\"/media/{id}\"></video></div>";

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

    /// <summary>
    /// Accordion-style TOC from markdown headings (h2–h4).
    /// Title and dir follow content language (majority RTL → فهرست مطالب).
    /// </summary>
    public string GenerateTableOfContents(string markdown, string cssClass = "post-toc", string? cultureCode = null)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;

        var headings = new List<(int Level, string Text, stringSlug, string Dir)>();
        foreach (Match m in HeadingRegex.Matches(markdown))
        {
            var level = m.Groups[1].Value.Length;
            if (level is < 2 or > 4) continue;

            var text = Regex.Replace(m.Groups[2].Value.Trim(), @"[*_`\[\]()#]", "").Trim();
            if (string.IsNullOrWhiteSpace(text) || text.Length < 2) continue;

            headings.Add((level, text,SlugifyHeading(text), DetectDir(text)));
        }

        if (headings.Count < 2) return string.Empty;

        var isRtlCulture = string.IsNullOrEmpty(cultureCode)
            || cultureCode.StartsWith("fa", StringComparison.OrdinalIgnoreCase)
            || cultureCode.StartsWith("ar", StringComparison.OrdinalIgnoreCase)
            || cultureCode.StartsWith("he", StringComparison.OrdinalIgnoreCase);

        var rtlCount = headings.Count(h => h.Dir == "rtl");
        var ltrCount = headings.Count(h => h.Dir == "ltr");
        var tocDir = ltrCount > rtlCount ? "ltr" : (rtlCount > 0 || isRtlCulture ? "rtl" : "ltr");

        var title = tocDir == "rtl" ? "فهرست مطالب" : "Table of contents";
        var openLabel = tocDir == "rtl" ? "باز کردن فهرست" : "Expand table of contents";
        var closeLabel = tocDir == "rtl" ? "بستن فهرست" : "Collapse table of contents";

        var minLevel = headings.Min(h => h.Level);
        var sb = new StringBuilder();

        sb.Append($"<nav class=\"{cssClass}\" data-toc dir=\"{tocDir}\" aria-label=\"{title}\">");
        sb.Append("<button type=\"button\" class=\"toc-toggle\" data-toc-toggle aria-expanded=\"false\"");
        sb.Append($" data-label-open=\"{openLabel}\" data-label-close=\"{closeLabel}\" aria-controls=\"toc-panel\">");
        sb.Append("<span class=\"toc-toggle-chevron\" aria-hidden=\"true\"></span>");
        sb.Append($"<span class=\"toc-title\">{title}</span>");
        sb.Append($"<span class=\"toc-count\" dir=\"ltr\">{headings.Count}</span>");
        sb.Append("</button>");
        sb.Append("<div class=\"toc-body\" id=\"toc-panel\" hidden>");
        sb.Append("<ol class=\"toc-list\">");

        var currentDepth = 0;
        var openLi = false;

        foreach (var (level, text, slug, dir) in headings)
        {
            var depth = level - minLevel;
            var encoded = System.Net.WebUtility.HtmlEncode(text);

            if (depth > currentDepth)
            {
                while (currentDepth < depth)
                {
                    sb.Append("<ol class=\"toc-list\">");
                    currentDepth++;
                    openLi = false;
                }
            }
            else if (depth < currentDepth)
            {
                if (openLi)
                {
                    sb.Append("</li>");
                    openLi = false;
                }

                while (currentDepth > depth)
                {
                    sb.Append("</ol></li>");
                    currentDepth--;
                }

                openLi = false;
            }
            else if (openLi)
            {
                sb.Append("</li>");
                openLi = false;
            }

            sb.Append($"<li class=\"toc-item toc-depth-{depth}\" dir=\"{dir}\">");
            sb.Append($"<a class=\"toc-link\" href=\"#{slug}\" dir=\"{dir}\">{encoded}</a>");
            openLi = true;
        }

        if (openLi) sb.Append("</li>");
        while (currentDepth > 0)
        {
            sb.Append("</ol></li>");
            currentDepth--;
        }

        sb.Append("</ol></div></nav>");
        return sb.ToString();
    }

    public string RenderToHtmlWithToc(string markdown, bool includeToc = true, string? cultureCode = null)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;
        var html = RenderToHtml(markdown);

        html = HeadingHtmlRegex.Replace(html, m =>
        {
            var level = m.Groups[1].Value;
            var attrs = m.Groups[2].Value ?? "";
            var inner = m.Groups[3].Value;
            var plain = StripTags(inner).Trim();
            var dir = DetectDir(plain);
            var id =SlugifyHeading(plain);
            attrs = Regex.Replace(attrs, "\\s*id\\s*=\\s*[\"'][^\"']*[\"']", "", RegexOptions.IgnoreCase);
            attrs = Regex.Replace(attrs, "\\s*dir\\s*=\\s*[\"'][^\"']*[\"']", "", RegexOptions.IgnoreCase);
            return $"<h{level} id=\"{id}\" dir=\"{dir}\" class=\"md-heading\"{attrs}>{inner}</h{level}>";
        });

        if (includeToc)
        {
            var toc = GenerateTableOfContents(markdown, "post-toc", cultureCode);
            if (!string.IsNullOrEmpty(toc)) html = toc + html;
        }

        return html;
    }

    public static string DetectDir(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "auto";
        foreach (var rune in text.EnumerateRunes())
        {
            var c = rune.Value;
            if (Rune.IsWhiteSpace(rune) || Rune.IsDigit(rune) || IsNeutral(c))
                continue;

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

    private static stringSlugifyHeading(string text)
    {
        var s = text.ToLowerInvariant().Trim();
        s = Regex.Replace(s, @"\s+", "-");
        s = Regex.Replace(s, "[^\\w\u0600-\u06FF\\-]", "");
        return s.Length > 80 ? s[..80] : s;
    }
}
