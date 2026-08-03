using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BlogApp.Services.Performance;
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

    private static readonly Regex TableBlockRegex =
        new(@"<table class=""md-table"">[\s\S]*?</table>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PreHtmlRegex =
        new(@"<pre(\s[^>]*)?>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex CodeHtmlRegex =
        new(@"<code(\s[^>]*)?>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ImgHtmlRegex =
        new(@"<img\s+([^>]*?)\s*/?>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex MediaPathRegex =
        new("(?:https?://[^/\\s\"']+)?/media/(\\d+)(?:/w/\\d+)?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex VideoEmbedDirRegex =
        new(@"<p dir=""(?:ltr|rtl|auto)"" class=""md-p"">\[\[VIDEO_EMBED_(\d+)\]\]</p>", RegexOptions.Compiled);

    private static readonly Regex VideoEmbedPlainRegex =
        new(@"<p>\[\[VIDEO_EMBED_(\d+)\]\]</p>", RegexOptions.Compiled);

    private static readonly Regex AttrLoadingRegex =
        new(@"\s*loading\s*=\s*[""'][^""']*[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AttrDecodingRegex =
        new(@"\s*decoding\s*=\s*[""'][^""']*[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AttrSrcsetRegex =
        new(@"\s*srcset\s*=\s*[""'][^""']*[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AttrSizesRegex =
        new(@"\s*sizes\s*=\s*[""'][^""']*[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AttrSrcRegex =
        new(@"src\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AttrClassRegex =
        new(@"class\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AttrIdRegex =
        new(@"\s*id\s*=\s*[""'][^""']*[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AttrDirRegex =
        new(@"\s*dir\s*=\s*[""'][^""']*[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly int[] SrcsetWidths = [480, 800, 1280];

    private readonly MarkdownPipeline _pipeline;
    private readonly ICdnUrlService _cdn;

    public MarkdownService(ICdnUrlService cdn)
    {
        _cdn = cdn;
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
        html = TableBlockRegex.Replace(html, m =>
            $"<div class=\"md-table-wrap\" tabindex=\"0\">{m.Value}</div>");

        html = ImgHtmlRegex.Replace(html, m => EnhanceImageTag(m.Groups[1].Value));

        html = VideoEmbedDirRegex.Replace(html, m => VideoEmbedHtml(m.Groups[1].Value));
        html = VideoEmbedPlainRegex.Replace(html, m => VideoEmbedHtml(m.Groups[1].Value));

        return html;
    }

    private string EnhanceImageTag(string attrs)
    {
        if (attrs.Contains("media-blur", StringComparison.OrdinalIgnoreCase)
            && attrs.Contains("srcset", StringComparison.OrdinalIgnoreCase))
            return $"<span class=\"media-blur-wrap\"><img {attrs} /></span>";

        attrs = AttrLoadingRegex.Replace(attrs, "");
        attrs = AttrDecodingRegex.Replace(attrs, "");
        attrs = AttrSrcsetRegex.Replace(attrs, "");
        attrs = AttrSizesRegex.Replace(attrs, "");

        var srcMatch = AttrSrcRegex.Match(attrs);
        if (srcMatch.Success)
        {
            var src = srcMatch.Groups[1].Value;
            var mediaId = TryParseMediaId(src);
            var resolved = _cdn.Resolve(src);
            if (!string.Equals(src, resolved, StringComparison.Ordinal))
                attrs = attrs.Replace(srcMatch.Value, $"src=\"{resolved}\"");

            if (mediaId is int mid)
            {
                var srcset = BuildSrcset(mid);
                attrs += $" srcset=\"{srcset}\" sizes=\"(max-width: 640px) 100vw, (max-width: 1024px) 90vw, 800px\"";
            }
        }

        if (AttrClassRegex.IsMatch(attrs))
            attrs = AttrClassRegex.Replace(attrs, "class=\"$1 media-blur\"");
        else
            attrs += " class=\"media-blur\"";

        return $"<span class=\"media-blur-wrap\"><img {attrs} loading=\"lazy\" decoding=\"async\" /></span>";
    }

    private string BuildSrcset(int mediaId)
    {
        var parts = new List<string>(SrcsetWidths.Length + 1);
        foreach (var w in SrcsetWidths)
            parts.Add($"{_cdn.Resolve($"/media/{mediaId}/w/{w}")} {w}w");
        parts.Add($"{_cdn.MediaUrl(mediaId)} 1920w");
        return string.Join(", ", parts);
    }

    private static int? TryParseMediaId(string url)
    {
        var m = MediaPathRegex.Match(url);
        if (m.Success && int.TryParse(m.Groups[1].Value, out var id))
            return id;
        return null;
    }

    private string VideoEmbedHtml(string id)
    {
        var src = _cdn.Resolve($"/media/{id}");
        return $"<div class=\"post-video-embed media-blur-wrap\"><video class=\"media-blur\" controls preload=\"metadata\" playsinline src=\"{src}\"></video></div>";
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

    public string GenerateTableOfContents(string markdown, string cssClass = "post-toc", string? cultureCode = null)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;

        var headings = new List<(int Level, string Text, string Slug, string Dir)>();
        foreach (Match m in HeadingRegex.Matches(markdown))
        {
            var level = m.Groups[1].Value.Length;
            var text = m.Groups[2].Value.Trim();
            if (string.IsNullOrEmpty(text)) continue;
            headings.Add((level, text,SlugifyHeading(text), DetectDir(text)));
        }

        if (headings.Count == 0) return string.Empty;

        var tocDir = cultureCode is "fa" or "ar" ? "rtl" : "ltr";
        var title = tocDir == "rtl" ? "فهرست مطالب" : "Table of contents";
        var openLabel = tocDir == "rtl" ? "باز کردن فهرست" : "Expand table of contents";
        var closeLabel = tocDir == "rtl" ? "بستن فهرست" : "Collapse table of contents";

        var sb = new StringBuilder();
        sb.Append($"<nav class=\"{cssClass}\" dir=\"{tocDir}\" aria-label=\"{title}\">");
        sb.Append($"<div class=\"post-toc-head\"><span class=\"post-toc-title\">{title}</span>");
        sb.Append($"<button type=\"button\" class=\"post-toc-toggle\" aria-expanded=\"true\" aria-label=\"{closeLabel}\" data-toc-toggle></button></div>");
        sb.Append("<ol class=\"post-toc-list\">");

        var stack = new Stack<int>();
        foreach (var (level, text, slug, dir) in headings)
        {
            while (stack.Count > 0 && stack.Peek() >= level)
            {
                sb.Append("</li>");
                stack.Pop();
                if (stack.Count > 0 && stack.Peek() >= level)
                    sb.Append("</ol>");
            }

            if (stack.Count > 0 && level > stack.Peek())
                sb.Append("<ol>");

            sb.Append($"<li><a href=\"#{slug}\" dir=\"{dir}\">{System.Net.WebUtility.HtmlEncode(text)}</a>");
            stack.Push(level);
        }
        while (stack.Count > 0)
        {
            sb.Append("</li>");
            stack.Pop();
            if (stack.Count > 0)
                sb.Append("</ol>");
        }

        sb.Append("</ol></nav>");
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
            var plain = StripTags(inner);
            var dir = DetectDir(plain);
            var slug =SlugifyHeading(plain);
            attrs = AttrIdRegex.Replace(attrs, "");
            attrs = AttrDirRegex.Replace(attrs, "");
            return $"<h{level} id=\"{slug}\" dir=\"{dir}\"{attrs}>{inner}</h{level}>";
        });

        if (includeToc)
        {
            var toc = GenerateTableOfContents(markdown, "post-toc", cultureCode);
            if (!string.IsNullOrEmpty(toc))
                html = toc + html;
        }

        return html;
    }

    public static string DetectDir(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "auto";
        var rtl = 0;
        var ltr = 0;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch) || char.IsDigit(ch) || IsSymbol(ch)) continue;
            var cat = char.GetUnicodeCategory(ch);
            if (cat is UnicodeCategory.OtherLetter)
            {
                if (ch is >= '\u0600' and <= '\u06FF'
                    or >= '\u0750' and <= '\u077F'
                    or >= '\u08A0' and <= '\u08FF'
                    or >= '\uFB50' and <= '\uFDFF'
                    or >= '\uFE70' and <= '\uFEFF'
                    or >= '\u0590' and <= '\u05FF')
                    rtl++;
                else
                    ltr++;
            }
            else if (cat is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter)
            {
                ltr++;
            }
        }
        if (rtl == 0 && ltr == 0) return "auto";
        return rtl >= ltr ? "rtl" : "ltr";
    }

    private static bool IsSymbol(char ch) =>
        char.GetUnicodeCategory(ch) is UnicodeCategory.MathSymbol
            or UnicodeCategory.CurrencySymbol
            or UnicodeCategory.ModifierSymbol
            or UnicodeCategory.OtherSymbol;

    private static string StripTags(string html) =>
        Regex.Replace(html, "<.*?>", string.Empty);

    private static bool IsVideoPlaceholder(string plain) =>
        plain.StartsWith("[[VIDEO_EMBED_", StringComparison.Ordinal);

    private static stringSlugifyHeading(string text) => slugifyHeading(text);

    private static string slugifyHeading(string text)
    {
        var s = text.ToLowerInvariant().Trim();
        s = Regex.Replace(s, @"\s+", "-");
        s = Regex.Replace(s, @"[^\w\u0600-\u06FF\-]", "");
        if (string.IsNullOrEmpty(s)) s = "section";
        return s.Length > 80 ? s[..80] : s;
    }
}
