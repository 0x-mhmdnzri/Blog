using System.Security.Claims;
using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using BlogApp.Services.Backup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BlogApp.Controllers;

[Authorize(Roles = AppRoles.SuperAdmin)]
[Route("AdminBackup")]
public class AdminBackupController : Controller
{
    private readonly IAppBackupService _backup;
    private readonly ApplicationDbContext _db;
    private readonly IOptions<BackupOptions> _options;
    private readonly IUiTranslator _t;
    private readonly ILogger<AdminBackupController> _log;

    public AdminBackupController(
        IAppBackupService backup,
        ApplicationDbContext db,
        IOptions<BackupOptions> options,
        IUiTranslator t,
        ILogger<AdminBackupController> log)
    {
        _backup = backup;
        _db = db;
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
                downloadUrl = Url.Action(nameof(Download), new { id = b.Id })
            })
        });
    }

    /// <summary>Server-side DataTables source for backup archive.</summary>
    [HttpGet("ArchiveData")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> ArchiveData()
    {
        var req = DataTablesRequest.From(Request);
        var query = _db.BackupRecords.AsNoTracking().AsQueryable();

        var total = await query.CountAsync();

        if (!string.IsNullOrWhiteSpace(req.SearchValue))
        {
            var term = req.SearchValue.Trim();
            query = query.Where(b =>
                b.FileName.Contains(term)
                || b.Kind.Contains(term)
                || (b.Notes != null && b.Notes.Contains(term)));
        }

        if (req.Col(0) is { } f)
            query = query.Where(b => b.FileName.Contains(f));
        if (req.Col(1) is { } k)
            query = query.Where(b => b.Kind.Contains(k));

        var filtered = await query.CountAsync();

        query = (req.OrderColumn, req.Asc) switch
        {
            (0, true) => query.OrderBy(b => b.FileName),
            (0, false) => query.OrderByDescending(b => b.FileName),
            (1, true) => query.OrderBy(b => b.Kind),
            (1, false) => query.OrderByDescending(b => b.Kind),
            (2, true) => query.OrderBy(b => b.SizeBytes),
            (2, false) => query.OrderByDescending(b => b.SizeBytes),
            (3, true) => query.OrderBy(b => b.CreatedAtUtc),
            (3, false) => query.OrderByDescending(b => b.CreatedAtUtc),
            _ => query.OrderByDescending(b => b.CreatedAtUtc)
        };

        var page = await query.Skip(req.Start).Take(req.Length).ToListAsync();

        var token = GetAntiforgeryToken();
        var dl = System.Net.WebUtility.HtmlEncode(_t["bk.download"]);
        var del = System.Net.WebUtility.HtmlEncode(_t["bk.delete"]);
        var confirm = System.Net.WebUtility.HtmlEncode(_t["bk.confirm_delete"]);

        var rows = page.Select((b, i) => new object[]
        {
            "<span class=\"ltr-field bk-mono\">" + System.Net.WebUtility.HtmlEncode(b.FileName) + "</span>",
            "<span class=\"bk-pill\">" + System.Net.WebUtility.HtmlEncode(b.Kind) + "</span>",
            FormatSize(b.SizeBytes),
            b.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm"),
            "<div class=\"bk-row-actions d-flex gap-1 flex-wrap\">" +
            "<a class=\"bk-btn bk-btn-sm bk-btn-primary\" href=\"/AdminBackup/Download/" + b.Id + "\">" + dl + "</a>" +
            "<form method=\"post\" action=\"/AdminBackup/Delete\" class=\"d-inline\" data-confirm=\"" + confirm + "\">" +
            "<input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"" + token + "\" />" +
            "<input type=\"hidden\" name=\"id\" value=\"" + b.Id + "\" />" +
            "<button type=\"submit\" class=\"bk-btn bk-btn-sm bk-btn-ghost\">" + del + "</button></form></div>"
        }).ToList();

        return Json(DataTablesResponse.Ok(req.Draw, total, filtered, rows));
    }

    private string GetAntiforgeryToken()
    {
        var af = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>();
        return af.GetAndStoreTokens(HttpContext).RequestToken ?? "";
    }

    private static string FormatSize(long bytes)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes;
        var i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return string.Format("{0:0.##} {1}", v, u[i]);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(bool download = true, CancellationToken ct = default)
    {
        try
        {
            var rec = await _backup.CreateBackupAsync(ActorId, "manual", ct);
            TempData["FlashOk"] = _t["bk.created_ok"];
            if (download)
                return RedirectToAction(nameof(Download), new { id = rec.Id });
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Backup create failed");
            TempData["FlashErr"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet("Download/{id:int}")]
    public async Task<IActionResult> Download(int id, CancellationToken ct)
    {
        try
        {
            var (stream, fileName, contentType) = await _backup.OpenDownloadAsync(id, ct);
            return File(stream, contentType, fileName);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Download failed {Id}", id);
            TempData["FlashErr"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var ok = await _backup.DeleteBackupAsync(id, ActorId, ct);
        TempData[ok ? "FlashOk" : "FlashErr"] = ok ? _t["bk.deleted_ok"] : _t["bk.deleted_fail"];
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Retention")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Retention(CancellationToken ct)
    {
        try
        {
            await _backup.EnforceRetentionAsync(ct);
            TempData["FlashOk"] = _t["bk.retention_ok"];
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Retention failed");
            TempData["FlashErr"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }
}
