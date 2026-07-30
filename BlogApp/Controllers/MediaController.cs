using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services;
using BlogApp.Services.Performance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace BlogApp.Controllers;

[Route("media")]
public class MediaController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<MediaController> _logger;
    private readonly IBackgroundJobQueue _jobs;
    private readonly ICdnUrlService _cdn;

    public MediaController(
        ApplicationDbContext db,
        ILogger<MediaController> logger,
        IBackgroundJobQueue jobs,
        ICdnUrlService cdn)
    {
        _db = db;
        _logger = logger;
        _jobs = jobs;
        _cdn = cdn;
    }

    [HttpGet("{id:int}")]
    [ResponseCache(Duration = 604800, Location = ResponseCacheLocation.Any, NoStore = false)]
    public async Task<IActionResult> Get(int id)
    {
        var asset = await _db.MediaAssets
            .AsNoTracking()
            .Select(m => new { m.Id, m.FileName, m.ContentType, m.Content, m.SizeBytes })
            .FirstOrDefaultAsync(m => m.Id == id);

        if (asset is null) return NotFound();

        var ct = string.IsNullOrWhiteSpace(asset.ContentType) ? "application/octet-stream" : asset.ContentType;
        if (ct.Contains("svg", StringComparison.OrdinalIgnoreCase)
            || ct.Contains("javascript", StringComparison.OrdinalIgnoreCase)
            || ct.Contains("html", StringComparison.OrdinalIgnoreCase))
        {
            ct = "application/octet-stream";
            Response.Headers[HeaderNames.ContentDisposition] = "attachment";
        }

        var etag = $"\"media-{asset.Id}-{asset.SizeBytes}\"";
        if (Request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var inm)
            && inm.ToString().Contains(etag, StringComparison.Ordinal))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers[HeaderNames.CacheControl] = "public,max-age=604800,immutable";
        Response.Headers[HeaderNames.ETag] = etag;
        Response.Headers[HeaderNames.AcceptRanges] = "bytes";
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        return File(asset.Content, ct, enableRangeProcessing: true);
    }

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpPost("upload")]
    [EnableRateLimiting("upload")]
    [RequestSizeLimit(SafeUpload.MaxVideoBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = SafeUpload.MaxVideoBytes)]
    public async Task<IActionResult> Upload(IFormFile file, int? postId)
    {
        var check = SafeUpload.Validate(file);
        if (!check.Ok)
        {
            _logger.LogWarning("Upload rejected User={User} Reason={Reason} Name={Name}",
                User.Identity?.Name, check.Error, file?.FileName);
            return BadRequest(new { error = check.Error });
        }

        if (postId is int pid)
        {
            var post = await _db.Posts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid);
            if (post is null) return BadRequest(new { error = "نوشته یافت نشد." });
            if (!AuthorAccess.OwnsPost(User, post)) return Forbid();
        }

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var bytes = ms.ToArray();

        if (bytes.Length >= 2 && bytes[0] == 0x4D && bytes[1] == 0x5A)
            return BadRequest(new { error = "نوع فایل اجرایی مجاز نیست." });

        var asset = new MediaAsset
        {
            FileName = check.SafeFileName,
            ContentType = check.ContentType,
            SizeBytes = bytes.LongLength,
            Content = bytes,
            Kind = check.Kind,
            PostId = postId
        };

        // Tracking required for insert
        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        _db.MediaAssets.Add(asset);
        await _db.SaveChangesAsync();

        if (check.Kind == MediaKind.Image)
            await _jobs.EnqueueImageOptimizeAsync(asset.Id);

        var url = _cdn.MediaUrl(asset.Id);
        var markdownSnippet = check.Kind switch
        {
            MediaKind.Image => $"![{Path.GetFileNameWithoutExtension(check.SafeFileName)}]({url})",
            MediaKind.Video => $"{{{{video:{asset.Id}}}}}",
            _ => $"[{check.SafeFileName}]({url})"
        };

        _logger.LogInformation("Upload ok User={User} MediaId={Id} Kind={Kind} Bytes={Bytes}",
            User.Identity?.Name, asset.Id, check.Kind, asset.SizeBytes);

        return Json(new { id = asset.Id, url, kind = check.Kind.ToString(), markdownSnippet });
    }
}
