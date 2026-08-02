using System.Security.Claims;
using BlogApp.Models;
using BlogApp.Services;
using BlogApp.Services.Backup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BlogApp.Controllers;

[Authorize(Roles = AppRoles.SuperAdmin)]
[Route("AdminBackup")]
public class AdminBackupController : Controller
{
    private readonly IAppBackupService _backup;
    private readonly IOptions<BackupOptions> _options;
    private readonly IUiTranslator _t;
    private readonly ILogger<AdminBackupController> _log;

    public AdminBackupController(
        IAppBackupService backup,
        IOptions<BackupOptions> options,
        IUiTranslator t,
        ILogger<AdminBackupController> log)
    {
        _backup = backup;
        _options = options;
        _t = t;
        _log = log;
    }

    private string ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "superadmin";

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = _t["admin.nav.backup"];
        var list = await _backup.ListAsync(ct);
        var snap = _backup.GetStorageSnapshot();
        ViewBag.Backups = list;
        ViewBag.Snapshot = snap;
        ViewBag.Options = _options.Value;
        return View();
    }

    [HttpGet("Stats")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Stats()
    {
        var snap = _backup.GetStorageSnapshot();
        Response.Headers["Cache-Control"] = "no-store, no-cache";
        return Json(snap);
    }

    /// <summary>REST list of backup files for live table refresh.</summary>
    [HttpGet("List")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var list = await _backup.ListAsync(ct);
        return Json(new
        {
            ok = true,
            at = DateTime.UtcNow,
            items = list.Select(b => new
            {
                id = b.Id,
                fileName = b.FileName,
                kind = b.Kind,
                sizeBytes = b.SizeBytes,
                createdAtUtc = b.CreatedAtUtc,
                downloadUrl = Url.Action(nameof(Download), new { id = b.Id }),
                deleteUrl = Url.Action(nameof(Delete))
            })
        });
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(bool download = true, CancellationToken ct = default)
    {
        try
        {
            var rec = await _backup.CreateFullBackupAsync(ActorId, "manual", ct);
            TempData["FlashOk"] = string.Format(
                _t["bk.flash_ready"], rec.FileName, FormatBytes(rec.SizeBytes));
            if (download)
                return RedirectToAction(nameof(Download), new { id = rec.Id });
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Manual full backup failed");
            TempData["FlashErr"] = string.Format(_t["bk.flash_failed"], ex.Message);
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet("Download/{id:int}")]
    public async Task<IActionResult> Download(int id, CancellationToken ct)
    {
        var path = _backup.GetBackupFilePath(id);
        if (path is null)
            return NotFound();
        var name = Path.GetFileName(path);
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return File(stream, "application/zip", name);
    }

    [HttpPost("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var ok = await _backup.DeleteBackupAsync(id, ActorId, ct);
        TempData[ok ? "FlashOk" : "FlashErr"] = ok ? _t["bk.flash_deleted"] : _t["bk.flash_delete_failed"];
        return RedirectToAction(nameof(Index));
    }

    private static string FormatBytes(long bytes)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes;
        var i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return $"{v:0.##} {u[i]}";
    }
}
