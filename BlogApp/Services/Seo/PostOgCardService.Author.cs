using System.Security.Cryptography;
using System.Text;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using IOPath = System.IO.Path;

namespace BlogApp.Services.Seo;

public sealed partial class PostOgCardService
{
    public Task InvalidatePostAsync(int postId, CancellationToken ct = default)
    {
        try
        {
            var dir = IOPath.Combine(_env.ContentRootPath, "App_Data", "og-cards");
            if (!Directory.Exists(dir)) return Task.CompletedTask;
            foreach (var old in Directory.EnumerateFiles(dir, $"{postId}-*.png"))
            {
                try { File.Delete(old); } catch { /* ignore */ }
            }
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "OG invalidate failed PostId={Id}", postId);
        }
        return Task.CompletedTask;
    }

    public async Task<byte[]?> GetOrCreateAuthorPngAsync(
        string userId,
        string displayName,
        string userName,
        string? bio,
        int postCount,
        int followerCount,
        long totalViews,
        CancellationToken ct = default)
    {
        try
        {
            var dir = IOPath.Combine(_env.ContentRootPath, "App_Data", "og-cards");
            Directory.CreateDirectory(dir);
            var hash = AuthorHash(userId, displayName, userName, bio, postCount, followerCount, totalViews);
            var safeId = new string((userId ?? "").Where(char.IsLetterOrDigit).Take(24).ToArray());
            if (string.IsNullOrEmpty(safeId)) safeId = "user";
            var path = IOPath.Combine(dir, $"author-{safeId}-{hash}.png");
            if (File.Exists(path))
                return await File.ReadAllBytesAsync(path, ct);

            foreach (var old in Directory.EnumerateFiles(dir, $"author-{safeId}-*.png"))
            {
                try { File.Delete(old); } catch { /* ignore */ }
            }

            var png = RenderAuthor(displayName, userName, bio, postCount, followerCount, totalViews);
            await File.WriteAllBytesAsync(path, png, ct);
            return png;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Author OG card render failed UserId={Id}", userId);
            return null;
        }
    }

    private byte[] RenderAuthor(
        string displayName,
        string userName,
        string? bio,
        int postCount,
        int followerCount,
        long totalViews)
    {
        var name = (displayName ?? "Author").Trim();
        if (name.Length > 48) name = name[..45] + "\u2026";
        var handle = string.IsNullOrWhiteSpace(userName) ? "" : "@" + userName.TrimStart('@');
        var summary = (bio ?? "").Trim();
        if (summary.Length > 110) summary = summary[..107] + "\u2026";

        var site = _seo.SiteName;
        var accent = AccentFrom(userName + name);
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
            ctx.Fill(panel, new RectangleF(32, 32, Width - 64, Height - 64));
            ctx.Draw(border, 2f, new RectangularPolygon(32, 32, Width - 64, Height - 64));
            ctx.Fill(accent, new RectangleF(32, 32, 8, Height - 64));

            var family = ResolveFontFamily();
            var brandFont = family.CreateFont(20, FontStyle.Regular);
            var titleFont = family.CreateFont(48, FontStyle.Bold);
            var bodyFont = family.CreateFont(22, FontStyle.Regular);
            var chipFont = family.CreateFont(20, FontStyle.Regular);
            var smallFont = family.CreateFont(18, FontStyle.Regular);

            ctx.DrawText(new RichTextOptions(brandFont)
            {
                Origin = new PointF(Width - 56, 56),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top
            }, site, muted);

            DrawChip(ctx, 64, 58, "Author", chipFont, chipBg, accent);

            ctx.DrawText(new RichTextOptions(titleFont)
            {
                Origin = new PointF(64, 130),
                WrappingLength = Width - 160,
                HorizontalAlignment = HorizontalAlignment.Left
            }, name, titleColor);

            if (!string.IsNullOrEmpty(handle))
            {
                ctx.DrawText(new RichTextOptions(bodyFont)
                {
                    Origin = new PointF(64, 210),
                    HorizontalAlignment = HorizontalAlignment.Left
                }, handle, muted);
            }

            if (!string.IsNullOrWhiteSpace(summary))
            {
                ctx.DrawText(new RichTextOptions(bodyFont)
                {
                    Origin = new PointF(64, 280),
                    WrappingLength = Width - 160,
                    HorizontalAlignment = HorizontalAlignment.Left
                }, summary, muted);
            }

            var statsY = Height - 150f;
            float x = 64;
            x = DrawStatChip(ctx, x, statsY, "posts", FormatCount(postCount), chipFont, chipBg, titleColor);
            x = DrawStatChip(ctx, x + 12, statsY, "followers", FormatCount(followerCount), chipFont, chipBg, titleColor);
            DrawStatChip(ctx, x + 12, statsY, "views", FormatCount((int)Math.Min(totalViews, int.MaxValue)), chipFont, chipBg, titleColor);

            ctx.DrawText(new RichTextOptions(smallFont)
            {
                Origin = new PointF(64, Height - 78),
                HorizontalAlignment = HorizontalAlignment.Left
            }, site, accent);
            ctx.Fill(accent, new RectangleF(64, Height - 58, 120, 4));
        });

        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    private static string AuthorHash(
        string userId, string displayName, string userName, string? bio,
        int postCount, int followerCount, long totalViews)
    {
        var viewTier = totalViews switch
        {
            < 10 => 0,
            < 100 => 1,
            < 1000 => 2,
            < 10000 => 3,
            _ => 4 + (int)(totalViews / 10000)
        };
        var raw = $"{userId}|{displayName}|{userName}|{bio}|{postCount}|{followerCount}|{viewTier}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash.AsSpan(0, 6)).ToLowerInvariant();
    }
}
