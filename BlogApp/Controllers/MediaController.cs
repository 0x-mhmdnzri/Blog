using BlogApp.Data;
using BlogApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace BlogApp.Controllers;

[Route("media")]
public class MediaController : Controller
{
    private readonly ApplicationDbContext _db;
    private static readonly HashSet<string> ImageTypes = new(StringComparer.OrdinalIgnoreCase)
        { "image/jpeg", "image/png", "image/gif", "image/webp", "image/svg+xml" };
    private static readonly HashSet<string> VideoTypes = new(StringComparer.OrdinalIgnoreCase)
        { "video/mp4", "video/webm", "video/ogg" };

    public MediaController(ApplicationDbContext db) => _db = db;

    /// <summary>
    /// Streams media from SQLite with range support, cache headers, and ETag.
    /// Already-compressed formats (jpeg/png/mp4) skip response compression via content-type.
    /// </summary>
    [HttpGet("{id:int}")]
    [ResponseCache(Duration = 604800, Location = ResponseCacheLocation.Any, NoStore = false)]
    public async Task<IActionResult> Get(int id)
    {
        var asset = await _db.MediaAssets
            .AsNoTracking()
            .Select(m => new { m.Id, m.FileName, m.ContentType, m.Content, m.SizeBytes })
            .FirstOrDefaultAsync(m => m.Id == id);

        if (asset is null) return NotFound();

        var etag = $"\"media-{asset.Id}-{asset.SizeBytes}\"";
        if (Request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var inm)
            && inm.ToString().Contains(etag, StringComparison.Ordinal))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers[HeaderNames.CacheControl] = "public,max-age=604800,immutable";
        Response.Headers[HeaderNames.ETag] = etag;
        Response.Headers[HeaderNames.AcceptRanges] = "bytes";

        return File(asset.Content, asset.ContentType, enableRangeProcessing: true);
    }

    [Authorize, HttpPost("upload")]
    [RequestSizeLimit(500L * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file, int? postId)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file received." });

        var kind = ImageTypes.Contains(file.ContentType) ? MediaKind.Image
            : VideoTypes.Contains(file.ContentType) ? MediaKind.Video
            : MediaKind.File;

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);

        var asset = new MediaAsset
        {
            FileName = file.FileName,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            SizeBytes = file.Length,
            Content = ms.ToArray(),
            Kind = kind,
            PostId = postId
        };

        _db.MediaAssets.Add(asset);
        await _db.SaveChangesAsync();

        var markdownSnippet = kind switch
        {
            MediaKind.Image => $"![{Path.GetFileNameWithoutExtension(file.FileName)}](/media/{asset.Id})",
            MediaKind.Video => $"{{{{video:{asset.Id}}}}}",
            _ => $"[{file.FileName}](/media/{asset.Id})"
        };

        return Json(new { id = asset.Id, url = $"/media/{asset.Id}", kind = kind.ToString(), markdownSnippet });
    }
}
