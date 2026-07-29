namespace BlogApp.Services;

/// <summary>
/// Magic-byte + extension allow-list for untrusted uploads (blocks polyglot / SVG XSS / executables).
/// </summary>
public static class SafeUpload
{
    public const long MaxImageBytes = 8L * 1024 * 1024;   // 8 MB
    public const long MaxVideoBytes = 200L * 1024 * 1024; // 200 MB
    public const long MaxFileBytes = 20L * 1024 * 1024;   // 20 MB generic (disabled by default)

    private static readonly HashSet<string> AllowedImageExt = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    private static readonly HashSet<string> AllowedVideoExt = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".webm", ".ogg", ".ogv" };

    // SVG intentionally excluded (stored XSS when served as image/svg+xml).

    public sealed record Result(bool Ok, string? Error, string ContentType, string SafeFileName, Models.MediaKind Kind);

    public static Result Validate(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return Fail("فایل خالی است.");

        var rawName = Path.GetFileName(file.FileName ?? "upload");
        var ext = Path.GetExtension(rawName);
        if (string.IsNullOrEmpty(ext) || ext.Contains('..', StringComparison.Ordinal))
            return Fail("پسوند فایل نامعتبر است.");

        // Read a header sample without loading the entire body twice when possible.
        Span<byte> header = stackalloc byte[32];
        int read;
        using (var stream = file.OpenReadStream())
        {
            read = stream.Read(header);
        }
        if (read < 4)
            return Fail("فایل خیلی کوتاه است.");

        var sample = header[..read];

        if (LooksLikeExecutable(sample))
            return Fail("نوع فایل اجرایی مجاز نیست.");

        // Disallow SVG even if Content-Type lies.
        if (IsSvg(sample, ext))
            return Fail("SVG به دلایل امنیتی پذیرفته نمی‌شود.");

        if (AllowedImageExt.Contains(ext) && IsImage(sample))
        {
            if (file.Length > MaxImageBytes)
                return Fail($"حداکثر حجم تصویر {MaxImageBytes / (1024 * 1024)} مگابایت است.");
            var ct = DetectImageContentType(sample, ext);
            return new Result(true, null, ct, SanitizeFileName(rawName, ext), Models.MediaKind.Image);
        }

        if (AllowedVideoExt.Contains(ext) && IsVideo(sample, ext))
        {
            if (file.Length > MaxVideoBytes)
                return Fail($"حداکثر حجم ویدیو {MaxVideoBytes / (1024 * 1024)} مگابایت است.");
            var ct = ext.ToLowerInvariant() switch
            {
                ".webm" => "video/webm",
                ".ogg" or ".ogv" => "video/ogg",
                _ => "video/mp4"
            };
            return new Result(true, null, ct, SanitizeFileName(rawName, ext), Models.MediaKind.Video);
        }

        return Fail("فقط تصویر (jpg/png/gif/webp) و ویدیو (mp4/webm/ogg) مجاز است.");
    }

    private static Result Fail(string error) =>
        new(false, error, "application/octet-stream", "blocked", Models.MediaKind.File);

    private static string SanitizeFileName(string name, string ext)
    {
        var baseName = Path.GetFileNameWithoutExtension(name);
        var clean = new string(baseName.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or ' ').ToArray()).Trim();
        if (clean.Length > 80) clean = clean[..80];
        if (string.IsNullOrWhiteSpace(clean)) clean = "file";
        return clean + ext.ToLowerInvariant();
    }

    private static bool LooksLikeExecutable(ReadOnlySpan<byte> h) =>
        // MZ (Windows PE), ELF, mach-O, shebang scripts
        (h.Length >= 2 && h[0] == 0x4D && h[1] == 0x5A)
        || (h.Length >= 4 && h[0] == 0x7F && h[1] == (byte)'E' && h[2] == (byte)'L' && h[3] == (byte)'F')
        || (h.Length >= 2 && h[0] == (byte)'#' && h[1] == (byte)'!');

    private static bool IsSvg(ReadOnlySpan<byte> h, string ext)
    {
        if (ext.Equals(".svg", StringComparison.OrdinalIgnoreCase))
            return true;
        // UTF-8 / UTF-16 BOM + "<svg" or "<?xml"
        var ascii = System.Text.Encoding.UTF8.GetString(h);
        return ascii.Contains("<svg", StringComparison.OrdinalIgnoreCase)
               || ascii.Contains("<?xml", StringComparison.OrdinalIgnoreCase) && ascii.Contains("svg", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsImage(ReadOnlySpan<byte> h)
    {
        // JPEG
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return true;
        // PNG
        if (h.Length >= 8 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47) return true;
        // GIF
        if (h.Length >= 6 && h[0] == (byte)'G' && h[1] == (byte)'I' && h[2] == (byte)'F') return true;
        // WEBP (RIFF....WEBP)
        if (h.Length >= 12 && h[0] == (byte)'R' && h[1] == (byte)'I' && h[2] == (byte)'F' && h[3] == (byte)'F'
            && h[8] == (byte)'W' && h[9] == (byte)'E' && h[10] == (byte)'B' && h[11] == (byte)'P') return true;
        return false;
    }

    private static string DetectImageContentType(ReadOnlySpan<byte> h, string ext)
    {
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8) return "image/jpeg";
        if (h.Length >= 4 && h[0] == 0x89 && h[1] == 0x50) return "image/png";
        if (h.Length >= 3 && h[0] == (byte)'G') return "image/gif";
        if (h.Length >= 12 && h[8] == (byte)'W') return "image/webp";
        return ext.ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };
    }

    private static bool IsVideo(ReadOnlySpan<byte> h, string ext)
    {
        // ISO BMFF (mp4/m4v): ....ftyp
        if (h.Length >= 8 && h[4] == (byte)'f' && h[5] == (byte)'t' && h[6] == (byte)'y' && h[7] == (byte)'p')
            return true;
        // WebM / Matroska EBML
        if (h.Length >= 4 && h[0] == 0x1A && h[1] == 0x45 && h[2] == 0xDF && h[3] == 0xA3)
            return true;
        // Ogg
        if (h.Length >= 4 && h[0] == (byte)'O' && h[1] == (byte)'g' && h[2] == (byte)'g' && h[3] == (byte)'S')
            return true;
        // Extension fallback only for known video ext after magic miss (some cameras)
        return AllowedVideoExt.Contains(ext);
    }
}
