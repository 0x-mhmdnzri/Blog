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
    public IActionResult Stats()
    {
        var snap = _backup.GetStorageSnapshot();
        return Json(snap);
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
        {
            TempData["FlashErr"] = _t["bk.flash_missing"];
            return RedirectToAction(nameof(Index));
        }

        var list = await _backup.ListAsync(ct);
        var rec = list.FirstOrDefault(b => b.Id == id);
        var name = rec?.FileName ?? Path.GetFileName(path);
        return PhysicalFile(path, "application/zip", name);
    }

    [HttpPost("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var ok = await _backup.DeleteBackupAsync(id, ActorId, ct);
        TempData[ok ? "FlashOk" : "FlashErr"] = ok ? _t["bk.flash_deleted"] : _t["bk.flash_not_found"];
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Retention")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Retention(CancellationToken ct)
    {
        var n = await _backup.EnforceRetentionAsync(ct);
        TempData["FlashOk"] = n == 0
            ? _t["bk.flash_purge_none"]
            : string.Format(_t["bk.flash_purged"], n);
        return RedirectToAction(nameof(Index));
    }

    private static string FormatBytes(long bytes)
    {
        string[] u = ["B", "KB", "MB", "GB", "TB"];
        double v = bytes;
        var i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return $"{v:0.##} {u[i]}";
    }
}
