using System.Text.RegularExpressions;
using BlogApp.Models;

namespace BlogApp.Services.Seo;

/// <summary>Unique per-post meta description, keywords for SEO/AEO.</summary>
public static class PostSeoMeta
{
    private static readonly Regex Ws = new(@"\s+", RegexOptions.Compiled);

    public static string BuildDescription(Post post, MarkdownService markdown, int maxLen = 160)
    {
        if (!string.IsNullOrWhiteSpace(post.Summary))
            return Truncate(Clean(post.Summary!), maxLen);

        var plain = markdown.ToPlainText(post.ContentMarkdown ?? "", maxLen + 40);
        plain = Clean(plain);
        if (string.IsNullOrWhiteSpace(plain))
            return Truncate(post.Title ?? "", maxLen);
        return Truncate(plain, maxLen);
    }

    public static string BuildKeywords(Post post)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return;
            foreach (var part in s.Split(new[] { ' ', ',', '\u060C', '|', '/', '-' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var p = part.Trim();
                if (p.Length < 2 || p.Length > 40) continue;
                set.Add(p);
            }
        }

        Add(post.Title);
        if (post.Category != null) Add(post.Category.Name);
        if (post.PostTags != null)
            foreach (var pt in post.PostTags)
                if (pt.Tag != null) Add(pt.Tag.Name);
        Add(post.Author?.DisplayName);

        return string.Join(", ", set.Take(16));
    }

    public static string BuildCanonical(HttpRequest request, Post post)
    {
        var baseUrl = $"{request.Scheme}://{request.Host}";
        return $"{baseUrl}/{post.LanguageCode}/post/{post.Slug}";
    }

    public static string BuildImageAlt(Post post, string? existingAlt, int imageIndex)
    {
        if (!string.IsNullOrWhiteSpace(existingAlt) && existingAlt.Trim().Length > 2)
            return existingAlt.Trim();

        var bits = new List<string>();
        if (!string.IsNullOrWhiteSpace(post.Title))
            bits.Add(post.Title.Trim());
        if (post.Category != null)
            bits.Add(post.Category.Name);
        var tag = post.PostTags?.Select(t => t.Tag?.Name).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
        if (tag != null) bits.Add(tag);

        var core = string.Join(" \u2014 ", bits.Where(b => !string.IsNullOrWhiteSpace(b)).Take(3));
        if (string.IsNullOrWhiteSpace(core))
            core = "\u062A\u0635\u0648\u06CC\u0631 \u0645\u0637\u0644\u0628";

        return imageIndex <= 1 ? core : $"{core} ({imageIndex})";
    }

    private static string Clean(string s) => Ws.Replace(s.Trim(), " ");

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
        var cut = s[..(max - 1)].TrimEnd();
        var sp = cut.LastIndexOf(' ');
        if (sp > max / 2) cut = cut[..sp];
        return cut + "\u2026";
    }
}
