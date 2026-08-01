using AVICRM.Data;
using AVICRM.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace AVICRM.Services.Performance;

/// <summary>
/// Image pipeline using ImageSharp: optional version snapshot, max-width resize,
/// WebP (or JPEG) re-encode, and responsive width variants for srcset.
/// </summary>
public sealed class ImageOptimizeService
{
    private static readonly int[] DefaultVariantWidths = [480, 800, 1280];

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

        var asset = await _db.MediaAssets
            .AsTracking()
            .Include(m => m.Variants)
            .FirstOrDefaultAsync(m => m.Id == mediaId, ct);

        if (asset is null || asset.Kind != MediaKind.Image || asset.Content.Length == 0)
            return;

        // Skip GIF/SVG/ICO — keep animated GIF and vector intact
        if (asset.ContentType.Contains("gif", StringComparison.OrdinalIgnoreCase)
            || asset.ContentType.Contains("svg", StringComparison.OrdinalIgnoreCase)
            || asset.ContentType.Contains("icon", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Image optimize skip MediaId={Id} CT={CT}", mediaId, asset.ContentType);
            return;
        }

        try
        {
            await using var input = new MemoryStream(asset.Content);
            using var image = await Image.LoadAsync(input, ct);

            var originalW = image.Width;
            var originalH = image.Height;

            // Snapshot current bytes as a version before first successful rewrite
            if (asset.OptimizedAtUtc is null)
            {
                _db.MediaVersions.Add(new MediaVersion
                {
                    MediaAssetId = asset.Id,
                    VersionNumber = asset.Version,
                    ContentType = asset.ContentType,
                    SizeBytes = asset.SizeBytes,
                    Content = asset.Content,
                    Width = originalW,
                    Height = originalH,
                    Note = "original-upload",
                    CreatedAtUtc = DateTime.UtcNow
                });
            }

            var maxW = Math.Max(320, _opt.MaxWidth);
            if (image.Width > maxW)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(maxW, 0),
                    Mode = ResizeMode.Max,
                    Sampler = KnownResamplers.Lanczos3
                }));
            }

            // Strip EXIF / metadata by not copying
            image.Metadata.ExifProfile = null;
            image.Metadata.IccProfile = null;
            image.Metadata.XmpProfile = null;

            var useWebP = _opt.PreferWebP;
            byte[] primaryBytes;
            string primaryCt;
            await using (var outMs = new MemoryStream())
            {
                if (useWebP)
                {
                    await image.SaveAsWebpAsync(outMs, new WebpEncoder
                    {
                        Quality = Math.Clamp(_opt.JpegQuality, 40, 100),
                        FileFormat = WebpFileFormatType.Lossy
                    }, ct);
                    primaryCt = "image/webp";
                }
                else
                {
                    await image.SaveAsJpegAsync(outMs, new JpegEncoder
                    {
                        Quality = Math.Clamp(_opt.JpegQuality, 40, 100)
                    }, ct);
                    primaryCt = "image/jpeg";
                }

                primaryBytes = outMs.ToArray();
            }

            // Only replace if smaller or format changed usefully
            if (primaryBytes.Length > 0 && (primaryBytes.Length < asset.Content.Length || useWebP))
            {
                asset.Content = primaryBytes;
                asset.SizeBytes = primaryBytes.LongLength;
                asset.ContentType = primaryCt;
                if (!asset.FileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) && useWebP)
                {
                    var baseName = Path.GetFileNameWithoutExtension(asset.FileName);
                    if (string.IsNullOrWhiteSpace(baseName)) baseName = "image";
                    asset.FileName = baseName + ".webp";
                }
            }

            asset.Width = image.Width;
            asset.Height = image.Height;
            asset.Version = Math.Max(1, asset.Version) + 1;
            asset.OptimizedAtUtc = DateTime.UtcNow;

            // Responsive variants
            var widths = (_opt.VariantWidths is { Length: > 0 } ? _opt.VariantWidths : DefaultVariantWidths)
                .Where(w => w > 0 && w < originalW)
                .Distinct()
                .OrderBy(w => w)
                .ToArray();

            if (asset.Variants.Count > 0)
                _db.MediaVariants.RemoveRange(asset.Variants);

            foreach (var targetW in widths)
            {
                using var clone = image.Clone(ctx => ctx.Resize(new ResizeOptions
                {
                    Size = new Size(targetW, 0),
                    Mode = ResizeMode.Max,
                    Sampler = KnownResamplers.Lanczos3
                }));

                await using var vms = new MemoryStream();
                string vCt;
                if (useWebP)
                {
                    await clone.SaveAsWebpAsync(vms, new WebpEncoder
                    {
                        Quality = Math.Clamp(_opt.JpegQuality, 40, 100),
                        FileFormat = WebpFileFormatType.Lossy
                    }, ct);
                    vCt = "image/webp";
                }
                else
                {
                    await clone.SaveAsJpegAsync(vms, new JpegEncoder
                    {
                        Quality = Math.Clamp(_opt.JpegQuality, 40, 100)
                    }, ct);
                    vCt = "image/jpeg";
                }

                var vBytes = vms.ToArray();
                _db.MediaVariants.Add(new MediaVariant
                {
                    MediaAssetId = asset.Id,
                    Width = clone.Width,
                    Height = clone.Height,
                    ContentType = vCt,
                    SizeBytes = vBytes.LongLength,
                    Content = vBytes,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Image optimized MediaId={Id} {Ow}x{Oh}→{W}x{H} bytes={Bytes} variants={V} CT={CT}",
                mediaId, originalW, originalH, asset.Width, asset.Height, asset.SizeBytes, widths.Length, asset.ContentType);
        }
        catch (UnknownImageFormatException ex)
        {
            _logger.LogWarning(ex, "Image optimize unknown format MediaId={Id}", mediaId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Image optimize failed MediaId={Id}", mediaId);
            throw;
        }
    }

    /// <summary>Restore a prior MediaVersion snapshot onto the live asset and clear variants.</summary>
    public async Task RestoreVersionAsync(int mediaId, int versionId, CancellationToken ct = default)
    {
        var asset = await _db.MediaAssets.AsTracking()
            .Include(m => m.Variants)
            .FirstOrDefaultAsync(m => m.Id == mediaId, ct);
        if (asset is null) return;

        var ver = await _db.MediaVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == versionId && v.MediaAssetId == mediaId, ct);
        if (ver is null) return;

        // Snapshot current before overwrite
        _db.MediaVersions.Add(new MediaVersion
        {
            MediaAssetId = asset.Id,
            VersionNumber = asset.Version,
            ContentType = asset.ContentType,
            SizeBytes = asset.SizeBytes,
            Content = asset.Content,
            Width = asset.Width,
            Height = asset.Height,
            Note = "pre-restore",
            CreatedAtUtc = DateTime.UtcNow
        });

        asset.Content = ver.Content;
        asset.ContentType = ver.ContentType;
        asset.SizeBytes = ver.SizeBytes;
        asset.Width = ver.Width;
        asset.Height = ver.Height;
        asset.Version = Math.Max(1, asset.Version) + 1;
        asset.OptimizedAtUtc = null;

        if (asset.Variants.Count > 0)
            _db.MediaVariants.RemoveRange(asset.Variants);

        await _db.SaveChangesAsync(ct);
    }
}
