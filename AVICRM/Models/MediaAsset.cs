using System.ComponentModel.DataAnnotations;

namespace AVICRM.Models;

/// <summary>
/// Any uploaded image, video, or file lives here as raw bytes in the database — nothing is
/// written to disk, so the DB really is the single source of truth for the whole blog.
/// Rendered back out through MediaController, which streams the bytes with the right
/// content-type and supports HTTP range requests (needed for video seeking).
/// </summary>
public class MediaAsset
{
    public int Id { get; set; }

    [Required, MaxLength(260)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string ContentType { get; set; } = "application/octet-stream";

    public long SizeBytes { get; set; }

    [Required]
    public byte[] Content { get; set; } = Array.Empty<byte>();

    public MediaKind Kind { get; set; }

    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;

    public int? PostId { get; set; }
    public Post? Post { get; set; }

    /// <summary>Pixel width when Kind == Image and known.</summary>
    public int? Width { get; set; }

    /// <summary>Pixel height when Kind == Image and known.</summary>
    public int? Height { get; set; }

    /// <summary>Monotonic version after each optimize/restore (starts at 1).</summary>
    public int Version { get; set; } = 1;

    /// <summary>When the image pipeline last rewrote Content.</summary>
    public DateTime? OptimizedAtUtc { get; set; }

    public ICollection<MediaVariant> Variants { get; set; } = new List<MediaVariant>();
    public ICollection<MediaVersion> Versions { get; set; } = new List<MediaVersion>();
}

public enum MediaKind
{
    Image,
    Video,
    File
}

/// <summary>Responsive width derivative of an image (srcset candidate).</summary>
public class MediaVariant
{
    public int Id { get; set; }

    public int MediaAssetId { get; set; }
    public MediaAsset? MediaAsset { get; set; }

    /// <summary>Target max width in CSS pixels (e.g. 480, 800, 1280).</summary>
    public int Width { get; set; }

    public int Height { get; set; }

    [Required, MaxLength(120)]
    public string ContentType { get; set; } = "image/webp";

    public long SizeBytes { get; set; }

    [Required]
    public byte[] Content { get; set; } = Array.Empty<byte>();

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Historical snapshot of a media asset for restore (file versioning).</summary>
public class MediaVersion
{
    public int Id { get; set; }

    public int MediaAssetId { get; set; }
    public MediaAsset? MediaAsset { get; set; }

    public int VersionNumber { get; set; }

    [Required, MaxLength(120)]
    public string ContentType { get; set; } = "application/octet-stream";

    public long SizeBytes { get; set; }

    [Required]
    public byte[] Content { get; set; } = Array.Empty<byte>();

    public int? Width { get; set; }
    public int? Height { get; set; }

    [MaxLength(200)]
    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
