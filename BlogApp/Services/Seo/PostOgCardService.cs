using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BlogApp.Models;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace BlogApp.Services.Seo;

public interface IPostOgCardService
{
    string GetCardUrl(Post post, HttpRequest request);
    Task<byte[]?> GetOrCreatePngAsync(Post post, CancellationToken ct = default);
    Task<byte[]?> GetOrCreateSitePngAsync(CancellationToken ct = default);
    string GetSiteCardUrl(HttpRequest request);
}

/// <summary>
/// GitHub-style 1200×630 Open Graph cards for social shares (Twitter/X, LinkedIn, Telegram).
/// Shows title, summary, views, likes, reading time, published date, category, author, site brand.
/// </summary>
public sealed class PostOgCardService : IPostOgCardService
{
    public const int Width = 1200;
    public const int Height = 630;

    private readonly IWebHostEnvironment _env;
    private readonly SeoService _seo;
    private readonly ILogger<PostOgCardService> _log;
    private static FontFamily? _family;
    private static readonly object FontLock = new();

    public PostOgCardService(IWebHostEnvironment env, SeoService seo, ILogger<PostOgCardService> log)
    {
        _env = env;
        _seo = seo;
        _log = log;
    }

    public string GetCardUrl(Post post, HttpRequest request)
    {
        var hash = ContentHash(post);
        return $"{request.Scheme}://{request.Host}/og/post/{post.Id}.png?v={hash}";
    }

    public string GetSiteCardUrl(HttpRequest request)
    {
        var hash = SiteHash();
        return $"{request.Scheme}://{request.Host}/og/site.png?v={hash}";
    }

    public async Task<byte[]?> GetOrCreatePngAsync(Post post, CancellationToken ct = default)
    {
        try
        {
            var dir = Path.Combine(_env.ContentRootPath, "App_Data", "og-cards");
            Directory.CreateDirectory(dir);
            var hash = ContentHash(post);
            var path = Path.Combine(dir, $"{post.Id}-{hash}.png");
            if (File.Exists(path))
                return await File.ReadAllBytesAsync(path, ct);

            foreach (var old in Directory.EnumerateFiles(dir, $"{post.Id}-*.png"))
            {
                try { File.Delete(old); } catch { /* ignore */ }
            }

            var png = RenderPost(post);
            await File.WriteAllBytesAsync(path, png, ct);
            return png;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "OG card render failed PostId={Id}", post.Id);
            return null;
        }
    }

    public async Task<byte[]?> GetOrCreateSitePngAsync(CancellationToken ct = default)
    {
        try
        {
            var dir = Path.Combine(_env.ContentRootPath, "App_Data", "og-cards");
            Directory.CreateDirectory(dir);
            var hash = SiteHash();
            var path = Path.Combine(dir, $"site-{hash}.png");
            if (File.Exists(path))
                return await File.ReadAllBytesAsync(path, ct);

            foreach (var old in Directory.EnumerateFiles(dir, "site-*.png"))
            {
                try { File.Delete(old); } catch { /* ignore */ }
            }

            var png = RenderSite();
            await File.WriteAllBytesAsync(path, png, ct);
            return png;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Site OG card render failed");
            return null;
        }
    }

    private byte[] RenderPost(Post post)
    {
        var title = (post.Title ?? "").Trim();
        if (title.Length > 88) title = title[..85] + "\u2026";

        var author = post.Author?.DisplayName ?? _seo.AuthorName;
        if (string.IsNullOrWhiteSpace(author)) author = "Author";
        var site = _seo.SiteName;
        var category = post.Category?.Name;

        var summary = !string.IsNullOrWhiteSpace(post.Summary)
            ? post.Summary!.Trim()
            : "";
        if (summary.Length > 120) summary = summary[..117] + "\u2026";

        var published = post.PublishedAtUtc ?? post.CreatedAtUtc;
        var dateStr = published.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

        var views = FormatCount(post.ViewCount);
        var likes = FormatCount(post.LikeCount);
        var readMin = post.ReadingTimeMinutes > 0 ? post.ReadingTimeMinutes : 1;

        var accent = AccentFrom(post.Slug + title);
        var bg = Color.ParseHex("0d1117");
        var panel = Color.ParseHex("161b22");
        var border = Color.ParseHex("30363d");
        var titleColor = Color.ParseHex("e6edf3");
        var muted = Color.ParseHex("8b949e");
        var chipBg = Color.ParseHex("21262d");

        using var image = new Image<Rgba32>(Width, Height);
        image.Mutate(ctx =>
        {
            ctx.Fill(bg);
            // Main panel with soft inset
            ctx.Fill(panel, new RectangleF(32, 32, Width - 64, Height - 64));
            ctx.Draw(border, 2, new RectangularPolygon(32, 32, Width - 64, Height - 64));

            // Left accent stripe
            ctx.Fill(accent, new RectangleF(32, 32, 8, Height - 64));

            // Top brand bar
            var family = ResolveFontFamily();
            var brandFont = family.CreateFont(20, FontStyle.Regular);
            var titleFont = family.CreateFont(46, FontStyle.Bold);
            var bodyFont = family.CreateFont(22, FontStyle.Regular);
            var chipFont = family.CreateFont(20, FontStyle.Regular);
            var smallFont = family.CreateFont(18, FontStyle.Regular);

            // Site name top-right
            var brandOpts = new RichTextOptions(brandFont)
            {
                Origin = new PointF(Width - 56, 56),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top
            };
            ctx.DrawText(brandOpts, site, muted);

            // Category pill top-left
            var labelY = 58f;
            if (!string.IsNullOrWhiteSpace(category))
            {
                var cat = category.Length > 28 ? category[..25] + "\u2026" : category;
                DrawChip(ctx, 64, labelY, cat, chipFont, chipBg, accent);
            }

            // Title
            var titleOpts = new RichTextOptions(titleFont)
            {
                Origin = new PointF(64, 120),
                WrappingLength = Width - 160,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                LineSpacing = 1.15f
            };
            ctx.DrawText(titleOpts, title, titleColor);

            // Summary under title
            if (!string.IsNullOrWhiteSpace(summary))
            {
                var subOpts = new RichTextOptions(bodyFont)
                {
                    Origin = new PointF(64, 320),
                    WrappingLength = Width - 160,
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                ctx.DrawText(subOpts, summary, muted);
            }

            // Stats row (GitHub-style)
            var statsY = Height - 150f;
            float x = 64;
            x = DrawStatChip(ctx, x, statsY, "views", views, chipFont, chipBg, muted, titleColor);
            x = DrawStatChip(ctx, x + 12, statsY, "likes", likes, chipFont, chipBg, muted, titleColor);
            x = DrawStatChip(ctx, x + 12, statsY, "read", $"{readMin} min", chipFont, chipBg, muted, titleColor);
            x = DrawStatChip(ctx, x + 12, statsY, "date", dateStr, chipFont, chipBg, muted, titleColor);

            // Footer: author · site
            var footer = $"{author}  \u00b7  {site}";
            var footOpts = new RichTextOptions(smallFont)
            {
                Origin = new PointF(64, Height - 78),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            ctx.DrawText(footOpts, footer, accent);

            // Accent underline under footer
            ctx.Fill(accent, new RectangleF(64, Height - 58, 120, 4));
        });

        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    private byte[] RenderSite()
    {
        var site = _seo.SiteName;
        var desc = _seo.SiteDescription;
        if (string.IsNullOrWhiteSpace(desc)) desc = "Blog";
        if (desc.Length > 140) desc = desc[..137] + "\u2026";

        var accent = Color.ParseHex("e3b341");
        var bg = Color.ParseHex("0d1117");
        var panel = Color.ParseHex("161b22");
        var border = Color.ParseHex("30363d");
        var titleColor = Color.ParseHex("e6edf3");
        var muted = Color.ParseHex("8b949e");

        using var image = new Image<Rgba32>(Width, Height);
        image.Mutate(ctx =>
        {
            ctx.Fill(bg);
            ctx.Fill(panel, new RectangleF(32, 32, Width - 64, Height - 64));
            ctx.Draw(border, 2, new RectangularPolygon(32, 32, Width - 64, Height - 64));
            ctx.Fill(accent, new RectangleF(32, 32, 8, Height - 64));

            var family = ResolveFontFamily();
            var titleFont = family.CreateFont(56, FontStyle.Bold);
            var bodyFont = family.CreateFont(26, FontStyle.Regular);
            var smallFont = family.CreateFont(20, FontStyle.Regular);

            var titleOpts = new RichTextOptions(titleFont)
            {
                Origin = new PointF(72, 200),
                WrappingLength = Width - 180,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            ctx.DrawText(titleOpts, site, titleColor);

            var subOpts = new RichTextOptions(bodyFont)
            {
                Origin = new PointF(72, 300),
                WrappingLength = Width - 180,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            ctx.DrawText(subOpts, desc, muted);

            var footOpts = new RichTextOptions(smallFont)
            {
                Origin = new PointF(72, Height - 90),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            ctx.DrawText(footOpts, "Open Graph  \u00b7  Share card", accent);
            ctx.Fill(accent, new RectangleF(72, Height - 68, 100, 4));
        });

        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    private static void DrawChip(
        IImageProcessingContext ctx,
        float x, float y,
        string text,
        Font font,
        Color bg,
        Color fg)
    {
        var measure = TextMeasurer.MeasureSize(text, new TextOptions(font));
        var padX = 14f;
        var padY = 8f;
        var w = measure.Width + padX * 2;
        var h = measure.Height + padY * 2;
        var rect = new RoundedRectangle(x, y, w, h, 8);
        ctx.Fill(bg, rect);
        ctx.DrawText(new RichTextOptions(font)
        {
            Origin = new PointF(x + padX, y + padY),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        }, text, fg);
    }

    private static float DrawStatChip(
        IImageProcessingContext ctx,
        float x, float y,
        string label,
        string value,
        Font font,
        Color bg,
        Color labelColor,
        Color valueColor)
    {
        var text = $"{label}  {value}";
        var measure = TextMeasurer.MeasureSize(text, new TextOptions(font));
        var padX = 16f;
        var padY = 10f;
        var w = measure.Width + padX * 2;
        var h = measure.Height + padY * 2;
        var rect = new RoundedRectangle(x, y, w, h, 10);
        ctx.Fill(bg, rect);

        // label in muted, value brighter — draw as one string with value emphasis via single color for simplicity
        ctx.DrawText(new RichTextOptions(font)
        {
            Origin = new PointF(x + padX, y + padY),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        }, text, valueColor);

        return x + w;
    }

    private static string FormatCount(int n)
    {
        if (n >= 1_000_000) return $"{n / 1_000_000.0:0.#}M";
        if (n >= 1_000) return $"{n / 1_000.0:0.#}k";
        return n.ToString(CultureInfo.InvariantCulture);
    }

    private static Color AccentFrom(string seed)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(seed ?? ""));
        var h = bytes[0] / 255f;
        if (h < 0.33f) return Color.ParseHex("e3b341");
        if (h < 0.66f) return Color.ParseHex("58a6ff");
        return Color.ParseHex("a371f7");
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
            foreach (var path in candidates)
            {
                if (!File.Exists(path)) continue;
                try
                {
                    var col = new FontCollection();
                    _family = col.Add(path);
                    return _family.Value;
                }
                catch { /* next */ }
            }
            _family = SystemFonts.Families.First();
            return _family.Value;
        }
    }

    /// <summary>Hash content fields + coarse view tier so cards refresh when stats jump.</summary>
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
        var raw = $"{post.Id}|{post.UpdatedAtUtc:O}|{post.Title}|{post.Summary}|{post.Author?.DisplayName}|{post.LikeCount}|{post.ReadingTimeMinutes}|{viewTier}|{post.PublishedAtUtc:yyyyMMdd}|{post.Category?.Name}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash.AsSpan(0, 6)).ToLowerInvariant();
    }

    private string SiteHash()
    {
        var raw = $"site|{_seo.SiteName}|{_seo.SiteDescription}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash.AsSpan(0, 6)).ToLowerInvariant();
    }

    private sealed class RoundedRectangle : IPath
    {
        private readonly IPath _path;

        public RoundedRectangle(float x, float y, float w, float h, float r)
        {
            var builder = new PathBuilder();
            builder.AddArc(new RectangleF(x, y, r * 2, r * 2), 180, 90);
            builder.AddArc(new RectangleF(x + w - r * 2, y, r * 2, r * 2), 270, 90);
            builder.AddArc(new RectangleF(x + w - r * 2, y + h - r * 2, r * 2, r * 2), 0, 90);
            builder.AddArc(new RectangleF(x, y + h - r * 2, r * 2, r * 2), 90, 90);
            builder.CloseFigure();
            _path = builder.Build();
        }

        public PathTypes PathType => _path.PathType;
        public RectangleF Bounds => _path.Bounds;
        public int MaxDegree => _path.MaxDegree;
        public IPath Transform(Matrix3x2 matrix) => _path.Transform(matrix);
        public IPath AsClosedPath() => _path.AsClosedPath();
        public IEnumerable<ISimplePath> Flatten() => _path.Flatten();
        public IEnumerable<ISimplePath> Flatten(float minLineLength) => _path.Flatten(minLineLength);
        public IEnumerable<ISimplePath> Flatten(float minLineLength, float maxCurveLength) =>
            _path.Flatten(minLineLength, maxCurveLength);
    }
}
