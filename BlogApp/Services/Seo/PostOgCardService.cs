using System.Security.Cryptography;
using System.Text;
using BlogApp.Models;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace BlogApp.Services.Seo;

public interface IPostOgCardService
{
    string GetCardUrl(Post post, HttpRequest request);
    Task<byte[]?> GetOrCreatePngAsync(Post post, CancellationToken ct = default);
}

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

            var png = Render(post);
            await File.WriteAllBytesAsync(path, png, ct);
            return png;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "OG card render failed PostId={Id}", post.Id);
            return null;
        }
    }

    private byte[] Render(Post post)
    {
        var title = (post.Title ?? "").Trim();
        if (title.Length > 90) title = title[..87] + "\u2026";
        var author = post.Author?.DisplayName ?? _seo.AuthorName;
        var site = _seo.SiteName;
        var subtitle = !string.IsNullOrWhiteSpace(post.Summary)
            ? post.Summary!.Trim()
            : (post.Category?.Name ?? site);
        if (subtitle.Length > 110) subtitle = subtitle[..107] + "\u2026";

        var accent = AccentFrom(post.Slug + title);
        var bg = Color.ParseHex("0b0e14");
        var panel = Color.ParseHex("12161f");
        var line = Color.ParseHex("1e2430");
        var titleColor = Color.ParseHex("e6e9f0");
        var muted = Color.ParseHex("8b93a7");

        using var image = new Image<Rgba32>(Width, Height);
        image.Mutate(ctx =>
        {
            ctx.Fill(bg);
            ctx.Fill(panel, new RectangleF(0, 0, Width, Height));
            ctx.Fill(accent, new RectangleF(0, 0, 14, Height));
            ctx.Fill(line, new RectangleF(0, 0, Width, 2));
            ctx.Fill(line, new RectangleF(0, Height - 2, Width, 2));
            ctx.Fill(accent, new RectangleF(Width - 280, Height - 8, 280, 8));

            var family = ResolveFontFamily();
            var titleFont = family.CreateFont(48, FontStyle.Bold);
            var metaFont = family.CreateFont(24, FontStyle.Regular);
            var brandFont = family.CreateFont(22, FontStyle.Regular);

            var titleOpts = new RichTextOptions(titleFont)
            {
                Origin = new PointF(64, 140),
                WrappingLength = Width - 140,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            ctx.DrawText(titleOpts, title, titleColor);

            var subOpts = new RichTextOptions(metaFont)
            {
                Origin = new PointF(64, 360),
                WrappingLength = Width - 140,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            ctx.DrawText(subOpts, subtitle, muted);

            var footer = string.IsNullOrWhiteSpace(author) ? site : $"{author}  \u00b7  {site}";
            var footOpts = new RichTextOptions(brandFont)
            {
                Origin = new PointF(64, Height - 78),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            ctx.DrawText(footOpts, footer, accent);

            ctx.Fill(accent, new RectangleF(64, Height - 52, 96, 4));
        });

        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    private static Color AccentFrom(string seed)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(seed ?? ""));
        var h = bytes[0] / 255f;
        if (h < 0.33f) return Color.ParseHex("e3b341");
        if (h < 0.66f) return Color.ParseHex("6fb3d2");
        return Color.ParseHex("c9a0f2");
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
                "/System/Library/Fonts/Supplemental/Arial Unicode.ttf",
                "/System/Library/Fonts/Supplemental/Arial.ttf",
                "C:/Windows/Fonts/arial.ttf",
                "C:/Windows/Fonts/tahoma.ttf"
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

    private static string ContentHash(Post post)
    {
        var raw = $"{post.Id}|{post.UpdatedAtUtc:O}|{post.Title}|{post.Summary}|{post.Author?.DisplayName}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash.AsSpan(0, 6)).ToLowerInvariant();
    }
}
