using BlogApp.Models;

namespace BlogApp.Models.ViewModels;

public class MediaLibraryViewModel
{
    public List<MediaLibraryItem> Items { get; set; } = new();
    public string? FilterKind { get; set; }
    public string? Search { get; set; }
    public int TotalCount { get; set; }
    public int ImageCount { get; set; }
    public int VideoCount { get; set; }
    public long TotalBytes { get; set; }
    public bool CanManageAll { get; set; }
}

public class MediaLibraryItem
{
    public int Id { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public MediaKind Kind { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public int? PostId { get; set; }
    public string? PostTitle { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int Version { get; set; }
    public DateTime? OptimizedAtUtc { get; set; }

    public string Url => $"/media/{Id}";

    public string SizeLabel
    {
        get
        {
            if (SizeBytes < 1024) return $"{SizeBytes} B";
            if (SizeBytes < 1024 * 1024) return $"{SizeBytes / 1024.0:0.#} KB";
            return $"{SizeBytes / (1024.0 * 1024):0.##} MB";
        }
    }
}
