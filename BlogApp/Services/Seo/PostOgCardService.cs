using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BlogApp.Models;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using IOPath = System.IO.Path;

namespace BlogApp.Services.Seo;

public interface IPostOgCardService
{
    string GetCardUrl(Post post, HttpRequest request);
    Task<byte[]?> GetOrCreatePngAsync(Post post, CancellationToken ct = default);
    Task<byte[]?> GetOrCreateJpegAsync(Post post, CancellationToken ct = default);
    Task<byte[]?> GetOrCreateSitePngAsync(CancellationToken ct = default);
    string GetSiteCardUrl(HttpRequest request);
    Task InvalidatePostAsync(int postId, CancellationToken ct = default);
    Task<byte[]?> GetOrCreateAuthorPngAsync(
        string userId,
        string displayName,
        string userName,
        string? bio,
        int postCount,
        int followerCount,
        long totalViews,
        CancellationToken ct = default);
}

/// <summary>
/// Shared 1200×630 Open Graph cards for X, LinkedIn, Telegram, WhatsApp, Discord, Slack, Facebook, etc.
/// GitHub-repo-style layout: category, language, title, summary, tags, views/likes/read/date, author brand.
/// </summary>
public sealed partial class PostOgCardService : IPostOgCardService
{
    public const int Width = 1200;
    public const int Height = 630;

    private readonly IWebHostEnvironment _env;
    private readonly SeoService _seo;
    private readonly ILogger<PostOgCardService> _log;
    private static FontFamily? _family;
    private static readonly object FontLock = new();

    // Shared palette (GitHub dark)
    private static readonly Color Bg = Color.ParseHex("0d1117");
    private static readonly Color Panel = Color.ParseHex("161b22");
    private static readonly Color Border = Color.ParseHex("30363d");
    private static readonly Color TitleColor = Color.ParseHex("e6edf3");
    private static readonly Color Muted = Color.ParseHex("8b949e");
    private static readonly Color ChipBg = Color.ParseHex("21262d");
    private static readonly Color StatValue = Color.ParseHex("f0f6fc");

    public PostOgCardService(IWebHostEnvironment env, SeoService seo, ILogger<PostOgCardService> log)
    {
        _env = env;
        _seo = seo;
        _log = log;
    }

    public string GetCardUrl(Post post, HttpRequest request)
    {
        var hash = ContentHash(post);
        // Absolute HTTPS URL — required by LinkedIn / WhatsApp / Telegram crawlers
        return $"{request.Scheme}://{request.Host}/og/post/{post.Id}.png?v={hash}";
    }

    public string GetSiteCardUrl(HttpRequest request)
    {
        var hash = SiteHash();
        return $"{request.Scheme}://{request.Host}/og/site.png?v={hash}";
    }

    public async Task<byte[]?> GetOrCreatePngAsync(Post post, CancellationToken ct = default)
        => await GetOrCreateBytesAsync(post, "png", ct);

    public async Task<byte[]?> GetOrCreateJpegAsync(Post post, CancellationToken ct = default)
        => await GetOrCreateBytesAsync(post, "jpg", ct);

    private async Task<byte[]?> GetOrCreateBytesAsync(Post post, string ext, CancellationToken ct)
    {
        try
        {
            var dir = IOPath.Combine(_env.ContentRootPath, "App_Data", "og-cards");
            Directory.CreateDirectory(dir);
            var hash = ContentHash(post);
            var path = IOPath.Combine(dir, $"{post.Id}-{hash}.{ext}");
            if (File.Exists(path))
                return await File.ReadAllBytesAsync(path, ct);

            foreach (var old in Directory.EnumerateFiles(dir, $"{post.Id}-*.{ext}"))
            {
                try { File.Delete(old); } catch { /* ignore */ }
            }

            using var image = RenderPostImage(post);
            await using var ms = new MemoryStream();
            if (ext == "jpg")
                await image.SaveAsJpegAsync(ms, new JpegEncoder { Quality = 90 }, ct);
            else
                await image.SaveAsPngAsync(ms, new PngEncoder { CompressionLevel = PngCompressionLevel.BestSpeed }, ct);

            var bytes = ms.ToArray();
            await File.WriteAllBytesAsync(path, bytes, ct);
            return bytes;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "OG card render failed PostId={Id} ext={Ext}", post.Id, ext);
            return null;
        }
    }

    public async Task<byte[]?> GetOrCreateSitePngAsync(CancellationToken ct = default)
    {
        try
        {
            var dir = IOPath.Combine(_env.ContentRootPath, "App_Data", "og-cards");
            Directory.CreateDirectory(dir);
            var hash = SiteHash();
            var path = IOPath.Combine(dir, $"site-{hash}.png");
            if (File.Exists(path))
                return await File.ReadAllBytesAsync(path, ct);

            foreach (var old in Directory.EnumerateFiles(dir, "site-*.png"))
            {
                try { File.Delete(old); } catch { /* ignore */ }
            }

            using var image = RenderSiteImage();
            await using var ms = new MemoryStream();
            await image.SaveAsPngAsync(ms, ct);
            var bytes = ms.ToArray();
            await File.WriteAllBytesAsync(path, bytes, ct);
            return bytes;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Site OG card render failed");
            return null;
        }
    }

    private Image<Rgba32> RenderPostImage(Post post)
    {
        var title = Truncate((post.Title ?? "").Trim(), 90);
        var author = post.Author?.DisplayName ?? _seo.AuthorName;
        if (string.IsNullOrWhiteSpace(author)) author = "Author";
        var site = _seo.SiteName;
        var category = post.Category?.Name;
        var lang = (post.LanguageCode ?? "fa").ToUpperInvariant();

        var summary = Truncate((post.Summary ?? "").Trim(), 130);
        var published = post.PublishedAtUtc ?? post.CreatedAtUtc;
        var dateStr = published.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

        var views = FormatCount(post.ViewCount);
        var likes = FormatCount(post.LikeCount);
        var readMin = post.ReadingTimeMinutes > 0 ? post.ReadingTimeMinutes : 1;

        var tags = post.PostTags?
            .Where(pt => pt.Tag != null && !string.IsNullOrWhiteSpace(pt.Tag.Name))
            .Select(pt => pt.Tag!.Name.Trim())
            .Take(4)
            .ToList() ?? new List<string>();

        var accent = AccentFrom(post.Slug + title);

        var image = new Image<Rgba32>(Width, Height);
        image.Mutate(ctx =>
        {
            DrawShell(ctx, accent);

            var family = ResolveFontFamily();
            var brandFont = family.CreateFont(20, FontStyle.Regular);
            var titleFont = family.CreateFont(44, FontStyle.Bold);
            var bodyFont = family.CreateFont(22, FontStyle.Regular);
            var chipFont = family.CreateFont(18, FontStyle.Regular);
            var smallFont = family.CreateFont(18, FontStyle.Regular);
            var statLabelFont = family.CreateFont(16, FontStyle.Regular);
            var statValueFont = family.CreateFont(22, FontStyle.Bold);

            // Brand top-right
            ctx.DrawText(new RichTextOptions(brandFont)
            {
                Origin = new PointF(Width - 56, 52),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top
            }, site, Muted);

            // Chips: language + category
            float chipX = 64;
            chipX = DrawChip(ctx, chipX, 52, lang, chipFont, ChipBg, accent) + 10;
            if (!string.IsNullOrWhiteSpace(category))
            {
                var cat = Truncate(category, 28);
                DrawChip(ctx, chipX, 52, cat, chipFont, ChipBg, TitleColor);
            }

            // Title
            ctx.DrawText(new RichTextOptions(titleFont)
            {
                Origin = new PointF(64, 110),
                WrappingLength = Width - 160,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                LineSpacing = 1.12f
            }, title, TitleColor);

            // Summary
            if (!string.IsNullOrWhiteSpace(summary))
            {
                ctx.DrawText(new RichTextOptions(bodyFont)
                {
                    Origin = new PointF(64, 290),
                    WrappingLength = Width - 160,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    LineSpacing = 1.2f
                }, summary, Muted);
            }

            // Tag chips
            if (tags.Count > 0)
            {
                float tx = 64;
                const float ty = 400;
                foreach (var t in tags)
                {
                    var label = "#" + Truncate(t, 18);
                    tx = DrawChip(ctx, tx, ty, label, chipFont, ChipBg, Muted) + 8;
                    if (tx > Width - 200) break;
                }
            }

            // Stats row — GitHub-repo style
            DrawStatsBar(ctx, 64, Height - 168,
                ("Views", views),
                ("Likes", likes),
                ("Read", $"{readMin} min"),
                ("Date", dateStr),
                statLabelFont, statValueFont, accent);

            // Footer
            var footer = $"{Truncate(author, 40)}  ·  {site}";
            ctx.DrawText(new RichTextOptions(smallFont)
            {
                Origin = new PointF(64, Height - 72),
                HorizontalAlignment = HorizontalAlignment.Left
            }, footer, accent);
            ctx.Fill(accent, new RectangleF(64, Height - 52, 100, 3));
        });

        return image;
    }

    private Image<Rgba32> RenderSiteImage()
    {
        var site = _seo.SiteName;
        var desc = Truncate(
            string.IsNullOrWhiteSpace(_seo.SiteDescription) ? "Blog" : _seo.SiteDescription,
            140);
        var accent = Color.ParseHex("e3b341");

        var image = new Image<Rgba32>(Width, Height);
        image.Mutate(ctx =>
        {
            DrawShell(ctx, accent);
            var family = ResolveFontFamily();
            var titleFont = family.CreateFont(52, FontStyle.Bold);
            var bodyFont = family.CreateFont(24, FontStyle.Regular);
            var smallFont = family.CreateFont(18, FontStyle.Regular);
            var chipFont = family.CreateFont(18, FontStyle.Regular);

            DrawChip(ctx, 64, 52, "SITE", chipFont, ChipBg, accent);

            ctx.DrawText(new RichTextOptions(titleFont)
            {
                Origin = new PointF(64, 180),
                WrappingLength = Width - 160,
                HorizontalAlignment = HorizontalAlignment.Left
            }, site, TitleColor);

            ctx.DrawText(new RichTextOptions(bodyFont)
            {
                Origin = new PointF(64, 280),
                WrappingLength = Width - 160,
                HorizontalAlignment = HorizontalAlignment.Left,
                LineSpacing = 1.25f
            }, desc, Muted);

            ctx.DrawText(new RichTextOptions(smallFont)
            {
                Origin = new PointF(64, Height - 72),
                HorizontalAlignment = HorizontalAlignment.Left
            }, "Open Graph · Share card", accent);
            ctx.Fill(accent, new RectangleF(64, Height - 52, 100, 3));
        });
        return image;
    }

    private static void DrawShell(IImageProcessingContext ctx, Color accent)
    {
        ctx.Fill(Bg);
        ctx.Fill(Panel, new RectangleF(28, 28, Width - 56, Height - 56));
        ctx.Draw(Border, 1.5f, new RectangularPolygon(28, 28, Width - 56, Height - 56));
        // Left accent bar
        ctx.Fill(accent, new RectangleF(28, 28, 7, Height - 56));
        // Subtle top highlight line
        ctx.Fill(Color.FromRgba(255, 255, 255, 12), new RectangleF(35, 28, Width - 63, 1));
    }

    private static void DrawStatsBar(
        IImageProcessingContext ctx,
        float x, float y,
        (string Label, string Value) a,
        (string Label, string Value) b,
        (string Label, string Value) c,
        (string Label, string Value) d,
        Font labelFont,
        Font valueFont,
        Color accent)
    {
        const float gap = 28f;
        float cursor = x;
        foreach (var (label, value) in new[] { a, b, c, d })
        {
            ctx.DrawText(new RichTextOptions(labelFont)
            {
                Origin = new PointF(cursor, y),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            }, label.ToUpperInvariant(), Muted);

            ctx.DrawText(new RichTextOptions(valueFont)
            {
                Origin = new PointF(cursor, y + 22),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            }, value, StatValue);

            var w = Math.Max(
                TextMeasurer.MeasureSize(label.ToUpperInvariant(), new TextOptions(labelFont)).Width,
                TextMeasurer.MeasureSize(value, new TextOptions(valueFont)).Width);
            cursor += w + gap + 16;

            // Divider between stats
            if (cursor < Width - 120)
                ctx.Fill(Border, new RectangleF(cursor - gap / 2 - 4, y + 4, 1, 42));
        }

        // Accent underline under stats region
        ctx.Fill(Color.FromRgba(accent.ToPixel<Rgba32>().R, accent.ToPixel<Rgba32>().G, accent.ToPixel<Rgba32>().B, 40),
            new RectangleF(x, y + 58, Math.Min(cursor - x, Width - 160), 2));
    }

    private static float DrawChip(
        IImageProcessingContext ctx,
        float x, float y,
        string text,
        Font font,
        Color bg,
        Color fg)
    {
        var measure = TextMeasurer.MeasureSize(text, new TextOptions(font));
        const float padX = 12f;
        const float padY = 6f;
        var w = measure.Width + padX * 2;
        var h = measure.Height + padY * 2;
        // Rounded-ish via fill rect (ImageSharp.Drawing rounded requires more deps)
        ctx.Fill(bg, new RectangleF(x, y, w, h));
        ctx.Draw(Border, 1f, new RectangularPolygon(x, y, w, h));
        ctx.DrawText(new RichTextOptions(font)
        {
            Origin = new PointF(x + padX, y + padY),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        }, text, fg);
        return x + w;
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
        return s[..(max - 1)].TrimEnd() + "\u2026";
    }

    private static string FormatCount(int n)
    {
        if (n >= 1_000_000) return $"{n / 1_000_000.0:0.#}M";
        if (n >= 1_000) return $"{n / 1_000.0:0.#}k";
        return n.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatCount(long n)
    {
        if (n >= 1_000_000) return $"{n / 1_000_000.0:0.#}M";
        if (n >= 1_000) return $"{n / 1_000.0:0.#}k";
        return n.ToString(CultureInfo.InvariantCulture);
    }

    private static Color AccentFrom(string seed)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(seed ?? ""));
        var h = bytes[0] / 255f;
        if (h < 0.33f) return Color.ParseHex("e3b341"); // gold
        if (h < 0.66f) return Color.ParseHex("58a6ff"); // blue
        return Color.ParseHex("a371f7"); // purple
    }

    private static FontFamily ResolveFontFamily()
    {
        if (_family is not null) return _family.Value;
        lock (FontLock)
        {
            if (_family is not null) return _family.Value;
            var candidates = new[]
            {
                "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
                "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
                "/usr/share/fonts/truetype/noto/NotoSans-Regular.ttf",
                "/usr/share/fonts/truetype/noto/NotoSansArabic-Regular.ttf",
                "/System/Library/Fonts/Supplemental/Arial Unicode.ttf",
                "/System/Library/Fonts/Supplemental/Arial.ttf",
                "C:/Windows/Fonts/arial.ttf",
                "C:/Windows/Fonts/tahoma.ttf",
                "C:/Windows/Fonts/segoeui.ttf"
            };
            foreach (var candidate in candidates)
            {
                if (!File.Exists(candidate)) continue;
                try
                {
                    var col = new FontCollection();
                    _family = col.Add(candidate);
                    return _family.Value;
                }
                catch { /* next */ }
            }
            _family = SystemFonts.Families.First();
            return _family.Value;
        }
    }

    private static string ContentHash(Post post)
    {
        var viewTier = post.ViewCount switch
        {
            < 10 => 0,
            < 50 => 1,
            < 100 => 2,
            < 500 => 3,
            < 1000 => 4,
            < 5000 => 5,
            < 10000 => 6,
            _ => 7 + post.ViewCount / 10000
        };
        var tags = post.PostTags == null
            ? ""
            : string.Join(",", post.PostTags.Where(t => t.Tag != null).Select(t => t.Tag!.Name).Take(4));
        var raw = $"{post.Id}|{post.UpdatedAtUtc:O}|{post.Title}|{post.Summary}|{post.Author?.DisplayName}|{post.LikeCount}|{post.ReadingTimeMinutes}|{viewTier}|{post.PublishedAtUtc:yyyyMMdd}|{post.Category?.Name}|{post.LanguageCode}|{tags}|v2";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash.AsSpan(0, 6)).ToLowerInvariant();
    }

    private string SiteHash()
    {
        var raw = $"site|{_seo.SiteName}|{_seo.SiteDescription}|v2";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash.AsSpan(0, 6)).ToLowerInvariant();
    }
}
