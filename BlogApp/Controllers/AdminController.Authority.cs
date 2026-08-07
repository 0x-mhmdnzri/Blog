using BlogApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public partial class AdminController
{
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> SeoAddBacklinkLead(
        string targetSite, string? targetUrl, string? ourUrl, string? contact,
        string status = "prospect", string? source = null, int? domainRating = null, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(targetSite))
        {
            TempData["SeoErr"] = "Target site is required.";
            return RedirectToAction(nameof(SeoTools), new { tab = "authority" });
        }
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "prospect", "contacted", "negotiated", "acquired", "lost", "rejected" };
        if (!allowed.Contains(status)) status = "prospect";
        _db.BacklinkLeads.Add(new BacklinkLead
        {
            TargetSite = targetSite.Trim(),
            TargetUrl = targetUrl?.Trim(),
            OurUrl = ourUrl?.Trim(),
            Contact = contact?.Trim(),
            Status = status.ToLowerInvariant(),
            Source = source?.Trim(),
            DomainRating = domainRating,
            Notes = notes?.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            AcquiredAtUtc = string.Equals(status, "acquired", StringComparison.OrdinalIgnoreCase) ? DateTime.UtcNow : null
        });
        await _db.SaveChangesAsync();
        TempData["SeoOk"] = "Backlink lead saved.";
        return RedirectToAction(nameof(SeoTools), new { tab = "authority" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> SeoUpdateBacklinkStatus(int id, string status)
    {
        var lead = await _db.BacklinkLeads.FindAsync(id);
        if (lead is null)
        {
            TempData["SeoErr"] = "Lead not found.";
            return RedirectToAction(nameof(SeoTools), new { tab = "authority" });
        }
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "prospect", "contacted", "negotiated", "acquired", "lost", "rejected" };
        if (!allowed.Contains(status)) status = lead.Status;
        lead.Status = status.ToLowerInvariant();
        lead.UpdatedAtUtc = DateTime.UtcNow;
        if (lead.Status == "acquired" && lead.AcquiredAtUtc is null)
            lead.AcquiredAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["SeoOk"] = "Status updated.";
        return RedirectToAction(nameof(SeoTools), new { tab = "authority" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> SeoDeleteBacklinkLead(int id)
    {
        var lead = await _db.BacklinkLeads.FindAsync(id);
        if (lead is not null)
        {
            _db.BacklinkLeads.Remove(lead);
            await _db.SaveChangesAsync();
            TempData["SeoOk"] = "Lead deleted.";
        }
        return RedirectToAction(nameof(SeoTools), new { tab = "authority" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> SeoAddAuthoritySnapshot(
        string period, string provider = "Ahrefs",
        int? domainRating = null, int? domainAuthority = null,
        int? trustFlow = null, int? citationFlow = null,
        int? referringDomains = null, int? organicKeywords = null, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(period))
        {
            var now = DateTime.UtcNow;
            var q = (now.Month - 1) / 3 + 1;
            period = $"{now.Year}-Q{q}";
        }
        _db.AuthoritySnapshots.Add(new AuthoritySnapshot
        {
            Period = period.Trim(),
            Provider = string.IsNullOrWhiteSpace(provider) ? "Ahrefs" : provider.Trim(),
            MeasuredAtUtc = DateTime.UtcNow,
            DomainRating = domainRating,
            DomainAuthority = domainAuthority,
            TrustFlow = trustFlow,
            CitationFlow = citationFlow,
            ReferringDomains = referringDomains,
            OrganicKeywords = organicKeywords,
            Notes = notes?.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        TempData["SeoOk"] = $"Authority snapshot {period} saved.";
        return RedirectToAction(nameof(SeoTools), new { tab = "authority" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> SeoDeleteAuthoritySnapshot(int id)
    {
        var row = await _db.AuthoritySnapshots.FindAsync(id);
        if (row is not null)
        {
            _db.AuthoritySnapshots.Remove(row);
            await _db.SaveChangesAsync();
            TempData["SeoOk"] = "Snapshot deleted.";
        }
        return RedirectToAction(nameof(SeoTools), new { tab = "authority" });
    }
}
