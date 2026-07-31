using System.Text;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using BlogApp.Services.Performance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public partial class AdminController
{
    [HttpGet]
    public async Task<IActionResult> Media(string? kind = null, string? q = null)
    {
        var userId = AuthorAccess.UserId(User)!;
        var seeAll = AuthorAccess.IsSuperAdmin(User);

        var query = _db.MediaAssets.AsNoTracking().AsQueryable();
        if (!seeAll)
            query = query.Where(m => m.PostId == null || _db.Posts.Any(p => p.Id == m.PostId && p.AuthorId == userId));

        if (!string.IsNullOrWhiteSpace(kind) && Enum.TryParse<MediaKind>(kind, true, out var k))
            query = query.Where(m => m.Kind == k);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            if (term.Length > 80) term = term[..80];
            query = query.Where(m => m.FileName.Contains(term) || m.ContentType.Contains(term));
        }

        var items = await query
            .OrderByDescending(m => m.UploadedAtUtc)
            .Take(120)
            .Select(m => new MediaLibraryItem
            {
                Id = m.Id,
                FileName = m.FileName,
                ContentType = m.ContentType,
                SizeBytes = m.SizeBytes,
                Kind = m.Kind,
                UploadedAtUtc = m.UploadedAtUtc,
                PostId = m.PostId,
                PostTitle = m.Post != null ? m.Post.Title : null,
                Width = m.Width,
                Height = m.Height,
                Version = m.Version,
                OptimizedAtUtc = m.OptimizedAtUtc
            })
            .ToListAsync();

        var statsQuery = _db.MediaAssets.AsNoTracking().AsQueryable();
        if (!seeAll)
            statsQuery = statsQuery.Where(m => m.PostId == null || _db.Posts.Any(p => p.Id == m.PostId && p.AuthorId == userId));

        var vm = new MediaLibraryViewModel
        {
            Items = items,
            FilterKind = kind,
            Search = q,
            TotalCount = await statsQuery.CountAsync(),
            ImageCount = await statsQuery.CountAsync(m => m.Kind == MediaKind.Image),
            VideoCount = await statsQuery.CountAsync(m => m.Kind == MediaKind.Video),
            TotalBytes = await statsQuery.SumAsync(m => (long?)m.SizeBytes) ?? 0,
            CanManageAll = seeAll
        };

        return View("Media", vm);
    }

    [HttpGet]
    public async Task<IActionResult> MediaData()
    {
        var req = DataTablesRequest.From(Request);
        var userId = AuthorAccess.UserId(User)!;
        var seeAll = AuthorAccess.IsSuperAdmin(User);

        var query = _db.MediaAssets.AsNoTracking().AsQueryable();
        if (!seeAll)
            query = query.Where(m => m.PostId == null || _db.Posts.Any(p => p.Id == m.PostId && p.AuthorId == userId));

        var total = await query.CountAsync();

        if (!string.IsNullOrWhiteSpace(req.SearchValue))
        {
            var term = req.SearchValue;
            if (term.Length > 80) term = term[..80];
            query = query.Where(m => m.FileName.Contains(term) || m.ContentType.Contains(term));
        }

        var kindFilter = Request.Query["kind"].ToString();
        if (!string.IsNullOrWhiteSpace(kindFilter) && Enum.TryParse<MediaKind>(kindFilter, true, out var kf))
            query = query.Where(m => m.Kind == kf);

        var filtered = await query.CountAsync();

        query = req.OrderColumn switch
        {
            1 => req.Asc ? query.OrderBy(m => m.FileName) : query.OrderByDescending(m => m.FileName),
            2 => req.Asc ? query.OrderBy(m => m.Kind) : query.OrderByDescending(m => m.Kind),
            3 => req.Asc ? query.OrderBy(m => m.SizeBytes) : query.OrderByDescending(m => m.SizeBytes),
            4 => req.Asc ? query.OrderBy(m => m.UploadedAtUtc) : query.OrderByDescending(m => m.UploadedAtUtc),
            _ => query.OrderByDescending(m => m.UploadedAtUtc)
        };

        var rows = await query
            .Skip(req.Start)
            .Take(req.Length)
            .Select(m => new
            {
                id = m.Id,
                fileName = m.FileName,
                contentType = m.ContentType,
                sizeBytes = m.SizeBytes,
                kind = m.Kind.ToString(),
                uploadedAt = m.UploadedAtUtc,
                postId = m.PostId,
                postTitle = m.Post != null ? m.Post.Title : null,
                url = "/media/" + m.Id,
                width = m.Width,
                height = m.Height,
                version = m.Version,
                optimized = m.OptimizedAtUtc != null
            })
            .ToListAsync();

        return Json(DataTablesResponse.Ok(req.Draw, total, filtered, rows));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [EnableRateLimiting("upload")]
    [RequestSizeLimit(SafeUpload.MaxVideoBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = SafeUpload.MaxVideoBytes)]
    public async Task<IActionResult> MediaUpload(IFormFile file, int? postId)
    {
        var check = SafeUpload.Validate(file);
        if (!check.Ok)
        {
            TempData["MediaErr"] = check.Error;
            return RedirectToAction(nameof(Media));
        }

        if (postId is int pid)
        {
            var post = await _db.Posts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid);
            if (post is null || !AuthorAccess.OwnsPost(User, post))
            {
                TempData["MediaErr"] = _t["media.err_post"];
                return RedirectToAction(nameof(Media));
            }
        }

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var bytes = ms.ToArray();

        if (bytes.Length >= 2 && bytes[0] == 0x4D && bytes[1] == 0x5A)
        {
            TempData["MediaErr"] = _t["media.err_exec"];
            return RedirectToAction(nameof(Media));
        }

        var asset = new MediaAsset
        {
            FileName = check.SafeFileName,
            ContentType = check.ContentType,
            SizeBytes = bytes.LongLength,
            Content = bytes,
            Kind = check.Kind,
            PostId = postId,
            UploadedAtUtc = DateTime.UtcNow,
            Version = 1
        };

        _db.MediaAssets.Add(asset);
        await _db.SaveChangesAsync();

        if (check.Kind == MediaKind.Image)
        {
            var jobs = HttpContext.RequestServices.GetRequiredService<IBackgroundJobQueue>();
            await jobs.EnqueueImageOptimizeAsync(asset.Id);
        }

        TempData["MediaOk"] = string.Format(_t["media.uploaded"], asset.Id);
        return RedirectToAction(nameof(Media));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MediaDelete(int id)
    {
        var asset = await _db.MediaAssets.FirstOrDefaultAsync(m => m.Id == id);
        if (asset is null) return RedirectToAction(nameof(Media));

        if (!await CanManageMediaAsync(asset))
            return Forbid();

        await _db.Posts.Where(p => p.CoverMediaAssetId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.CoverMediaAssetId, (int?)null));

        _db.MediaAssets.Remove(asset);
        await _db.SaveChangesAsync();

        TempData["MediaOk"] = _t["media.deleted"];
        return RedirectToAction(nameof(Media));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MediaBulkDelete(int[] ids)
    {
        if (ids is null || ids.Length == 0)
            return RedirectToAction(nameof(Media));

        var distinct = ids.Distinct().Take(100).ToArray();
        var assets = await _db.MediaAssets.Where(m => distinct.Contains(m.Id)).ToListAsync();
        var removed = 0;

        foreach (var asset in assets)
        {
            if (!await CanManageMediaAsync(asset)) continue;
            await _db.Posts.Where(p => p.CoverMediaAssetId == asset.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.CoverMediaAssetId, (int?)null));
            _db.MediaAssets.Remove(asset);
            removed++;
        }

        await _db.SaveChangesAsync();
        TempData["MediaOk"] = string.Format(_t["media.bulk_deleted"], removed);
        return RedirectToAction(nameof(Media));
    }

    [HttpGet]
    public async Task<IActionResult> MediaUsage(int id)
    {
        var asset = await _db.MediaAssets.AsNoTracking()
            .Select(m => new { m.Id, m.FileName, m.PostId })
            .FirstOrDefaultAsync(m => m.Id == id);
        if (asset is null) return NotFound();

        var needle = $"/media/{id}";
        var posts = await _db.Posts.AsNoTracking()
            .Where(p => !p.IsDeleted && (
                p.CoverMediaAssetId == id
                || p.ContentMarkdown.Contains(needle)
                || (p.Summary != null && p.Summary.Contains(needle))))
            .Select(p => new { p.Id, p.Title, p.Slug, isCover = p.CoverMediaAssetId == id })
            .Take(50)
            .ToListAsync();

        return Json(new
        {
            id = asset.Id,
            fileName = asset.FileName,
            usages = posts.Select(p => new
            {
                p.Id,
                p.Title,
                p.Slug,
                p.isCover,
                editUrl = Url.Action("Edit", "Posts", new { id = p.Id })
            })
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MediaReoptimize(int id)
    {
        var asset = await _db.MediaAssets.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
        if (asset is null) return RedirectToAction(nameof(Media));
        if (!await CanManageMediaAsync(asset)) return Forbid();
        if (asset.Kind != MediaKind.Image)
        {
            TempData["MediaErr"] = _t["media.err_not_image"];
            return RedirectToAction(nameof(Media));
        }

        var jobs = HttpContext.RequestServices.GetRequiredService<IBackgroundJobQueue>();
        await jobs.EnqueueImageOptimizeAsync(id);
        TempData["MediaOk"] = string.Format(_t["media.reoptimize_queued"], id);
        return RedirectToAction(nameof(Media));
    }

    [HttpGet]
    public async Task<IActionResult> MediaVersions(int id)
    {
        var asset = await _db.MediaAssets.AsNoTracking()
            .Select(m => new { m.Id, m.FileName, m.Kind, m.Version })
            .FirstOrDefaultAsync(m => m.Id == id);
        if (asset is null) return NotFound();

        var full = await _db.MediaAssets.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
        if (full is null || !await CanManageMediaAsync(full)) return Forbid();

        var versions = await _db.MediaVersions.AsNoTracking()
            .Where(v => v.MediaAssetId == id)
            .OrderByDescending(v => v.CreatedAtUtc)
            .Select(v => new
            {
                v.Id,
                v.VersionNumber,
                v.ContentType,
                v.SizeBytes,
                v.Width,
                v.Height,
                v.Note,
                v.CreatedAtUtc
            })
            .Take(50)
            .ToListAsync();

        var variants = await _db.MediaVariants.AsNoTracking()
            .Where(v => v.MediaAssetId == id)
            .OrderBy(v => v.Width)
            .Select(v => new { v.Id, v.Width, v.Height, v.ContentType, v.SizeBytes })
            .ToListAsync();

        return Json(new
        {
            id = asset.Id,
            fileName = asset.FileName,
            currentVersion = asset.Version,
            versions,
            variants
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MediaRestoreVersion(int id, int versionId)
    {
        var asset = await _db.MediaAssets.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
        if (asset is null) return RedirectToAction(nameof(Media));
        if (!await CanManageMediaAsync(asset)) return Forbid();

        var optimizer = HttpContext.RequestServices.GetRequiredService<ImageOptimizeService>();
        await optimizer.RestoreVersionAsync(id, versionId);

        TempData["MediaOk"] = string.Format(_t["media.version_restored"], versionId);
        return RedirectToAction(nameof(Media));
    }

    private async Task<bool> CanManageMediaAsync(MediaAsset asset)
    {
        if (AuthorAccess.IsSuperAdmin(User)) return true;
        if (asset.PostId is null) return true;
        var post = await _db.Posts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == asset.PostId);
        return post is not null && AuthorAccess.OwnsPost(User, post);
    }
}
