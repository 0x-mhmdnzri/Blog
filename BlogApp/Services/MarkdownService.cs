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

    private static readonly Regex TableCellHtmlRegex =
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
        new("(?:https?://[^\"']*/)?(?:/)?media/file/(\\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex VideoEmbedRegex =
        new(@"\[\[VIDEO_EMBED_(\d+)\]\]", RegexOptions.Compiled);

    private static readonly Regex VideoEmbedPlainRegex =
        new(@"<p>\[\[VIDEO_EMBED_(\d+)\]\]</p>", RegexOptions.Compiled);

    private static readonly Regex AttrLoadingRegex =
        new(@"\s*loading\s*=\s*[\"'][^\"']*[\"']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AttrDecodingRegex =
        new(@"\s*decoding\s*=\s*[\"'][^\"']*[\"']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AttrSrcsetRegex =
        new(@"\s*srcset\s*=\s*[\"'][^\"']*[\"']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AttrSizesRegex =
        new(@"\s*sizes\s*=\s*[\"'][^\"']*[\"']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AttrSrcRegex =
        new(@"src\s*=\s*[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AttrClassRegex =
        new(@"class\s*=\s*[\"']([^\"']*)[\"']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AttrIdRegex =
        new(@"\s*id\s*=\s*[\"'][^\"']*[\"']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AttrDirRegex =
        new(@"\s*dir\s*=\s*[\"'][^\"']*[\"']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

        // Preserve language-* classes for highlight.js; always LTR for source
        html = PreHtmlRegex.Replace(html, m =>
        {
            var attrs = m.Groups[1].Success ? m.Groups[1].Value : "";
            if (AttrClassRegex.IsMatch(attrs))
                attrs = AttrClassRegex.Replace(attrs, " class=\"$1 md-code-block\"");
            else
                attrs += " class=\"md-code-block\"";
            if (!attrs.Contains("dir=", StringComparison.OrdinalIgnoreCase))
                attrs += " dir=\"ltr\"";
            return "<pre" + attrs + ">";
        });
        html = CodeHtmlRegex.Replace(html, m =>
        {
            var attrs = m.Groups[1].Success ? m.Groups[1].Value : "";
            if (!attrs.Contains("dir=", StringComparison.OrdinalIgnoreCase))
                attrs += " dir=\"ltr\"";
            return "<code" + attrs + ">";
        });

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
        html = TableCellHtmlRegex.Replace(html, m =>
        {
            var tag = m.Groups[1].Value;
            var attrs = m.Groups[2].Success ? m.Groups[2].Value : "";
            var inner = m.Groups[3].Value;
            var plain = StripTags(inner);
            var dir = DetectDir(plain);
            attrs = AttrDirRegex.Replace(attrs, "");
            return $"<{tag}{attrs} dir=\"{dir}\">{inner}</{tag}>";
        });
        html = TableBlockRegex.Replace(html, m =>
            $"<div class=\"md-table-wrap\" tabindex=\"0\">{m.Value}</div>");

        html = ImgHtmlRegex.Replace(html, m => EnhanceImageTag(m.Groups[1].Value));
        html = VideoEmbedPlainRegex.Replace(html, m => EmbedVideo(m.Groups[1].Value));
        html = VideoEmbedRegex.Replace(html, m => EmbedVideo(m.Groups[1].Value));

        return html;
    }

    private static bool IsVideoPlaceholder(string plain) =>
        plain.StartsWith("[[VIDEO_EMBED_", StringComparison.Ordinal) && plain.EndsWith("]]", StringComparison.Ordinal);

    private string EnhanceImageTag(string attrs)
    {
        attrs = AttrLoadingRegex.Replace(attrs, "");
        attrs = AttrDecodingRegex.Replace(attrs, "");

        var srcMatch = AttrSrcRegex.Match(attrs);
        if (!srcMatch.Success)
            return $"<img {attrs} loading=\"lazy\" decoding=\"async\" />";

        var src = srcMatch.Groups[1].Value;
        var mediaMatch = MediaPathRegex.Match(src);
        if (mediaMatch.Success && int.TryParse(mediaMatch.Groups[1].Value, out var mediaId))
        {
            try
            {
                var cdn = _cdn.GetFileUrl(mediaId);
                if (!string.IsNullOrEmpty(cdn))
                    attrs = AttrSrcRegex.Replace(attrs, $"src=\"{cdn}\"");
            }
            catch { /* ignore */ }
        }

        if (AttrClassRegex.IsMatch(attrs))
            attrs = AttrClassRegex.Replace(attrs, "class=\"$1 media-blur\"");
        else
            attrs += " class=\"media-blur\"";

        return $"<span class=\"media-blur-wrap\"><img {attrs} loading=\"lazy\" decoding=\"async\" /></span>";
    }

    private string EmbedVideo(string mediaId)
    {
        var src = $"/media/file/{mediaId}";
        try
        {
            if (int.TryParse(mediaId, out var id))
            {
                var cdn = _cdn.GetFileUrl(id);
                if (!string.IsNullOrEmpty(cdn)) src = cdn;
            }
        }
        catch { /* ignore */ }

        return $"<div class=\"post-video-embed media-blur-wrap\"><video class=\"media-blur\" controls preload=\"metadata\" playsinline src=\"{src}\"></video></div>";
    }

    private static string StripTags(string html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        return Regex.Replace(html, "<[^>]+>", string.Empty);
    }

    public string RenderToHtmlWithToc(string markdown, bool includeToc = true, string? cultureCode = null)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;
        var html = RenderToHtml(markdown);
        if (!includeToc) return html;
        var toc = BuildTocHtml(markdown, cultureCode);
        if (string.IsNullOrEmpty(toc)) return html;
        return toc + html;
    }

    public string BuildTocHtml(string markdown, string? cultureCode = null)
    {
        var headings = new List<(int Level, string Text, string Slug, string Dir)>();
        foreach (Match m in HeadingRegex.Matches(markdown ?? string.Empty))
        {
            var level = m.Groups[1].Value.Length;
            var text = m.Groups[2].Value.Trim();
            if (string.IsNullOrEmpty(text)) continue;
            headings.Add((level, text, SlugifyHeading(text), DetectDir(text)));
        }
        if (headings.Count < 2) return string.Empty;

        var title = cultureCode is "en" ? "On this page" : cultureCode is "ar" ? "في هذه الصفحة" : "در این صفحه";
        var closeLabel = cultureCode is "en" ? "Toggle" : "بستن";
        var tocDir = headings.Count > 0 && headings.All(h => h.Dir == "ltr") ? "ltr" : "rtl";
        var cssClass = "post-toc";

        var sb = new StringBuilder();
        sb.Append($"<nav class=\"{cssClass}\" dir=\"{tocDir}\" aria-label=\"{title}\">");
        sb.Append($"<div class=\"post-toc-head\"><span class=\"post-toc-title\">{title}</span>");
        sb.Append($"<button type=\"button\" class=\"post-toc-toggle\" aria-expanded=\"true\" aria-label=\"{closeLabel}\" data-toc-toggle></button></div>");
        sb.Append("<ol class=\"post-toc-list\">");
        var prev = 0;
        foreach (var (level, text, slug, dir) in headings)
        {
            if (prev > 0 && level > prev)
            {
                for (var i = prev; i < level; i++) sb.Append("<ol>");
            }
            else if (prev > 0 && level < prev)
            {
                for (var i = level; i < prev; i++) sb.Append("</ol></li>");
            }
            else if (prev > 0)
            {
                sb.Append("</li>");
            }
            sb.Append($"<li><a href=\"#{slug}\" dir=\"{dir}\">{System.Net.WebUtility.HtmlEncode(text)}</a>");
            prev = level;
        }
        while (prev > 1) { sb.Append("</li></ol>"); prev--; }
        if (prev > 0) sb.Append("</li>");
        sb.Append("</ol></nav>");
        return sb.ToString();
    }

    public static string DetectDir(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "auto";
        var rtl = 0;
        var ltr = 0;
        foreach (var ch in text)
        {
            var cat = char.GetUnicodeCategory(ch);
            if (cat is UnicodeCategory.OtherLetter)
            {
                if (ch is >= '\u0600' and <= '\u06FF' or >= '\u0750' and <= '\u077F' or >= '\u08A0' and <= '\u08FF'
                    or >= '\uFB50' and <= '\uFDFF' or >= '\uFE70' and <= '\uFEFF')
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

    private static string SlugifyHeading(string text)
    {
        var s = text.Trim().ToLowerInvariant();
        s = Regex.Replace(s, @"\s+", "-");
        s = Regex.Replace(s, @"[^\w\u0600-\u06FF\-]", "");
        if (string.IsNullOrEmpty(s)) s = "section";
        return s.Length > 80 ? s[..80] : s;
    }
}
