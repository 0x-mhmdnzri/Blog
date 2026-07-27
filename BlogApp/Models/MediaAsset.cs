using System.ComponentModel.DataAnnotations;

namespace BlogApp.Models;

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
}

public enum MediaKind
{
    Image,
    Video,
    File
}
