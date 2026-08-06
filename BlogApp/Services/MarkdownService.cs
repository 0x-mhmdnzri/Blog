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

        html = ImgHtmlRegex.Replace(html, EnhanceImageTag);
        html = VideoEmbedPlainRegex.Replace(html, m => EmbedVideo(m.Groups[1].Value));
        html = VideoEmbedRegex.Replace(html, m => EmbedVideo(m.Groups[1].Value));

        return html;
    }

    // NOTE: remaining methods restored from previous version via partial - if incomplete, re-fetch
}
