using System.Security.Cryptography;
using System.Text;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
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
            foreach (var old in Directory.EnumerateFiles(dir, $"{postId}-*.*"))
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

            using var image = RenderAuthorImage(displayName, userName, bio, postCount, followerCount, totalViews);
            await using var ms = new MemoryStream();
            await image.SaveAsPngAsync(ms, ct);
            var bytes = ms.ToArray();
            await File.WriteAllBytesAsync(path, bytes, ct);
            return bytes;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Author OG card render failed UserId={Id}", userId);
            return null;
        }
    }

    private Image<Rgba32> RenderAuthorImage(
        string displayName,
        string userName,
        string? bio,
        int postCount,
        int followerCount,
        long totalViews)
    {
        var name = Truncate((displayName ?? "Author").Trim(), 48);
        var handle = string.IsNullOrWhiteSpace(userName) ? "" : "@" + userName.TrimStart('@');
        var summary = Truncate((bio ?? "").Trim(), 120);
        var site = _seo.SiteName;
        var accent = AccentFrom(userName + name);

        var image = new Image<Rgba32>(Width, Height);
        image.Mutate(ctx =>
        {
            DrawShell(ctx, accent);

            var family = ResolveFontFamily();
            var brandFont = family.CreateFont(20, FontStyle.Regular);
            var titleFont = family.CreateFont(46, FontStyle.Bold);
            var bodyFont = family.CreateFont(22, FontStyle.Regular);
            var chipFont = family.CreateFont(18, FontStyle.Regular);
            var smallFont = family.CreateFont(18, FontStyle.Regular);
            var statLabelFont = family.CreateFont(16, FontStyle.Regular);
            var statValueFont = family.CreateFont(22, FontStyle.Bold);

            ctx.DrawText(new RichTextOptions(brandFont)
            {
                Origin = new PointF(Width - 56, 52),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top
            }, site, Muted);

            DrawChip(ctx, 64, 52, "AUTHOR", chipFont, ChipBg, accent);

            ctx.DrawText(new RichTextOptions(titleFont)
            {
                Origin = new PointF(64, 120),
                WrappingLength = Width - 160,
                HorizontalAlignment = HorizontalAlignment.Left
            }, name, TitleColor);

            if (!string.IsNullOrEmpty(handle))
            {
                ctx.DrawText(new RichTextOptions(bodyFont)
                {
                    Origin = new PointF(64, 200),
                    HorizontalAlignment = HorizontalAlignment.Left
                }, handle, Muted);
            }

            if (!string.IsNullOrWhiteSpace(summary))
            {
                ctx.DrawText(new RichTextOptions(bodyFont)
                {
                    Origin = new PointF(64, 260),
                    WrappingLength = Width - 160,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    LineSpacing = 1.2f
                }, summary, Muted);
            }

            DrawStatsBar(ctx, 64, Height - 168,
                ("Posts", FormatCount(postCount)),
                ("Followers", FormatCount(followerCount)),
                ("Views", FormatCount(totalViews)),
                ("Profile", handle.Length > 0 ? handle : "—"),
                statLabelFont, statValueFont, accent);

            ctx.DrawText(new RichTextOptions(smallFont)
            {
                Origin = new PointF(64, Height - 72),
                HorizontalAlignment = HorizontalAlignment.Left
            }, site, accent);
            ctx.Fill(accent, new RectangleF(64, Height - 52, 100, 3));
        });

        return image;
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
        var raw = $"{userId}|{displayName}|{userName}|{bio}|{postCount}|{followerCount}|{viewTier}|v2";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash.AsSpan(0, 6)).ToLowerInvariant();
    }
}
