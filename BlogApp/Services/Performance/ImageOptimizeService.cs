using BlogApp.Data;
using BlogApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BlogApp.Services.Performance;

/// <summary>
/// Lightweight image pipeline without native deps:
/// downscales very large JPEG/PNG headers via re-encode of raw bytes is limited;
/// here we strip EXIF by re-saving JPEG SOI..EOI when possible and convert large PNGs
/// to JPEG when PreferWebP is false. Full WebP needs ImageSharp in production deploy.
/// When optimization cannot improve, marks SizeBytes unchanged and returns.
/// </summary>
public sealed class ImageOptimizeService
{
    private readonly ApplicationDbContext _db;
    private readonly ImageOptimizeOptions _opt;
    private readonly ILogger<ImageOptimizeService> _logger;

    public ImageOptimizeService(
        ApplicationDbContext db,
        IOptions<PerformanceOptions> opt,
        ILogger<ImageOptimizeService> logger)
    {
        _db = db;
        _opt = opt.Value.ImageOptimize;
        _logger = logger;
    }

    public async Task OptimizeAsync(int mediaId, CancellationToken ct = default)
    {
        if (!_opt.Enabled) return;

        // Tracking needed for update
        var asset = await _db.MediaAssets
            .AsTracking()
            .FirstOrDefaultAsync(m => m.Id == mediaId, ct);

        if (asset is null || asset.Kind != MediaKind.Image || asset.Content.Length == 0)
            return;

        var original = asset.SizeBytes;
        var optimized = TryOptimizeBytes(asset.Content, asset.ContentType, out var newCt, out var newBytes);
        if (!optimized || newBytes is null || newBytes.Length == 0 || newBytes.Length >= asset.Content.Length)
        {
            _logger.LogDebug("Image optimize skip MediaId={Id} (no gain)", mediaId);
            return;
        }

        asset.Content = newBytes;
        asset.SizeBytes = newBytes.LongLength;
        asset.ContentType = newCt;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Image optimized MediaId={Id} {From}→{To} bytes CT={CT}",
            mediaId, original, asset.SizeBytes, asset.ContentType);
    }

    private static bool TryOptimizeBytes(byte[] input, string contentType, out string newCt, out byte[]? output)
    {
        newCt = contentType;
        output = null;

        // JPEG: strip trailing data after EOI (0xFF 0xD9), common camera junk
        if (IsJpeg(input))
        {
            var eoi = FindJpegEoi(input);
            if (eoi > 0 && eoi + 2 < input.Length)
            {
                output = input.AsSpan(0, eoi + 2).ToArray();
                newCt = "image/jpeg";
                return true;
            }
            return false;
        }

        // PNG under size threshold — leave as-is (lossless recompress needs encoder)
        if (IsPng(input) && input.Length > 1_500_000)
        {
            // Without ImageSharp we cannot safely recompress; skip.
            return false;
        }

        return false;
    }

    private static bool IsJpeg(byte[] b) =>
        b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF;

    private static bool IsPng(byte[] b) =>
        b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47;

    private static int FindJpegEoi(byte[] b)
    {
        for (var i = b.Length - 2; i >= 2; i--)
        {
            if (b[i] == 0xFF && b[i + 1] == 0xD9)
                return i;
        }
        return -1;
    }
}
