using System.Text.RegularExpressions;
using Markdig;

namespace BlogApp.Services;

/// <summary>
/// Turns an author's raw README-style Markdown into safe, styled HTML.
/// Uses Markdig's "advanced" extension bundle (GitHub-flavored): tables, fenced code with
/// language info, auto-links, task lists, footnotes, definition lists, emoji, pipe tables,
/// and soft-line-break-as-newline — the same feature set as a GitHub README.
/// Images and file links point at /media/{id}, which is served straight out of the database.
/// A custom {{video:ID}} token embeds an uploaded video inline as an HTML5 <video> player.
/// </summary>
public class MarkdownService
{
    private static readonly Regex VideoTokenRegex =
        new(@"\{\{\s*video\s*:\s*(\d+)\s*\}\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly MarkdownPipeline _pipeline;

    public MarkdownService()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()   // tables, footnotes, task lists, definition lists, etc.
            .UseEmojiAndSmiley()
            .UseSoftlineBreakAsHardlineBreak()
            .UseAutoLinks()
            .DisableHtml()             // authors write Markdown, not raw HTML — keeps output safe
            .Build();
    }

    /// <summary>Renders full README-style Markdown to HTML, with video tokens expanded first.</summary>
    public string RenderToHtml(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;

        // Raw HTML is disabled in the pipeline for safety, so we can't drop an HTML comment
        // in as a placeholder (it would just get escaped). Use a plain-text marker on its own
        // paragraph instead — Markdig wraps it in a <p>, which we swap out after rendering.
        var withVideos = VideoTokenRegex.Replace(markdown, match =>
        {
            var mediaId = match.Groups[1].Value;
            return $"\n\n[[VIDEO_EMBED_{mediaId}]]\n\n";
        });

        var html = Markdown.ToHtml(withVideos, _pipeline);

        html = Regex.Replace(html, @"<p>\[\[VIDEO_EMBED_(\d+)\]\]</p>", m =>
        {
            var id = m.Groups[1].Value;
            return $"""
                <div class="post-video-embed">
                    <video controls preload="metadata" src="/media/{id}"></video>
                </div>
                """;
        });

        return html;
    }

    /// <summary>Strips Markdown down to plain text, used for auto-generating summaries/excerpts.</summary>
    public string ToPlainText(string markdown, int maxLength = 200)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;
        var html = Markdown.ToPlainText(markdown, _pipeline);
        var plain = Regex.Replace(html, @"\s+", " ").Trim();
        return plain.Length <= maxLength ? plain : plain[..maxLength].TrimEnd() + "…";
    }
}
