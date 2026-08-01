using BlogApp.Models;
using BlogApp.Models.Enterprise;
using BlogApp.Services;
using BlogApp.Services.Enterprise;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BlogApp.Controllers;

[Authorize(Roles = AppRoles.SuperAdmin)]
[Route("AdminEnterprise")]
public class AdminEnterpriseController : Controller
{
    private readonly IEnterpriseService _ent;

    public AdminEnterpriseController(IEnterpriseService ent) => _ent = ent;

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Enterprise";
        ViewBag.Tenants = await _ent.ListTenantsAsync(ct);
        ViewBag.Approvals = await _ent.ListApprovalsAsync(null, ct);
        ViewBag.Backups = await _ent.ListBackupsAsync(ct);
        ViewBag.Sso = await _ent.GetSsoAsync(null, ct);
        ViewBag.Localization = await _ent.ListLocalizationAsync(null, ct);
        return View();
    }

    [HttpPost("CreateTenant")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTenant(string code, string name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            TempData["FlashErr"] = "Code and name required";
            return RedirectToAction(nameof(Index));
        }
        await _ent.CreateTenantAsync(code, name, ct);
        TempData["FlashOk"] = "Tenant created";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("CreateWorkspace")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateWorkspace(int tenantId, string code, string name, bool isolated = true, CancellationToken ct = default)
    {
        await _ent.CreateWorkspaceAsync(tenantId, code, name, isolated, ct);
        TempData["FlashOk"] = "Workspace created";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("AddDomain")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddDomain(int tenantId, string host, bool primary = false, CancellationToken ct = default)
    {
        var d = await _ent.AddDomainAsync(tenantId, host, primary, ct);
        TempData["FlashOk"] = $"Domain added. Verify with token: {d.VerificationToken}";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("VerifyDomain")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyDomain(int domainId, string token, CancellationToken ct)
    {
        var ok = await _ent.VerifyDomainAsync(domainId, token, ct);
        TempData[ok ? "FlashOk" : "FlashErr"] = ok ? "Domain verified" : "Invalid token";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("SaveSso")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSso(SsoProviderConfig model, CancellationToken ct)
    {
        await _ent.SaveSsoAsync(model, ct);
        TempData["FlashOk"] = "SSO config saved (enable OpenIdConnect registration at deploy time with these values)";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("ResolveApproval")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveApproval(int id, bool approve, string? notes, CancellationToken ct)
    {
        try
        {
            await _ent.ResolveApprovalAsync(id, UserId, approve, notes, ct);
            TempData["FlashOk"] = approve ? "Approved & published" : "Rejected";
        }
        catch (Exception ex)
        {
            TempData["FlashErr"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("LegalHold")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LegalHold(int? postId, string? targetUserId, string reason, CancellationToken ct)
    {
        await _ent.PlaceLegalHoldAsync(postId, targetUserId, reason, UserId, ct);
        TempData["FlashOk"] = "Legal hold placed";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("ReleaseHold")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReleaseHold(int holdId, CancellationToken ct)
    {
        await _ent.ReleaseLegalHoldAsync(holdId, UserId, ct);
        TempData["FlashOk"] = "Hold released";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("GdprExport")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GdprExport(string targetUserId, CancellationToken ct)
    {
        var json = await _ent.BuildGdprExportJsonAsync(targetUserId, ct);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        return File(bytes, "application/json", $"gdpr-{targetUserId}.json");
    }

    [HttpPost("GdprErase")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GdprErase(string targetUserId, CancellationToken ct)
    {
        try
        {
            await _ent.EraseUserDataAsync(targetUserId, UserId, ct);
            TempData["FlashOk"] = "User data erased / anonymized";
        }
        catch (Exception ex)
        {
            TempData["FlashErr"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Backup")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Backup(CancellationToken ct)
    {
        var b = await _ent.CreateBackupAsync(UserId, ct);
        TempData["FlashOk"] = $"Backup {b.FileName} ({b.SizeBytes} bytes)";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Restore")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int backupId, CancellationToken ct)
    {
        try
        {
            await _ent.RestoreBackupAsync(backupId, UserId, ct);
            TempData["FlashOk"] = "Backup extracted to App_Data/restore-staging — restart to apply";
        }
        catch (Exception ex)
        {
            TempData["FlashErr"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Localization")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Localization(string key, string languageCode, string value, string status = "draft", CancellationToken ct = default)
    {
        await _ent.UpsertLocalizationAsync(key, languageCode, value, status, null, ct);
        TempData["FlashOk"] = "Localization entry saved";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("DrRunbook")]
    public IActionResult DrRunbook()
    {
        ViewData["Title"] = "Disaster Recovery";
        return View();
    }
}
