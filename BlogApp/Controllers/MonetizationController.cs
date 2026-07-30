using System.ComponentModel.DataAnnotations;
using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public class MonetizationController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IUiTranslator _t;
    private readonly IMembershipService _membership;

    public MonetizationController(ApplicationDbContext db, IUiTranslator t, IMembershipService membership)
    {
        _db = db;
        _t = t;
        _membership = membership;
    }

    [HttpGet]
    public async Task<IActionResult> Donate()
    {
        ViewData["Title"] = _t["mon.donate_title"];
        ViewBag.Recent = await _db.Donations.AsNoTracking()
            .Where(d => d.Status == DonationStatus.Confirmed && !d.IsAnonymous)
            .OrderByDescending(d => d.ConfirmedAtUtc)
            .Take(12)
            .Select(d => new { d.DonorName, d.Amount, d.Currency, d.Message })
            .ToListAsync();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Donate(DonateForm model)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = _t["mon.donate_title"];
            return View(model);
        }

        _db.Donations.Add(new Donation
        {
            UserId = AuthorAccess.UserId(User),
            DonorName = model.IsAnonymous ? null : model.DonorName?.Trim(),
            DonorEmail = model.DonorEmail?.Trim(),
            Amount = model.Amount,
            Currency = string.IsNullOrWhiteSpace(model.Currency) ? "IRT" : model.Currency.Trim().ToUpperInvariant(),
            Message = model.Message?.Trim(),
            IsAnonymous = model.IsAnonymous,
            Status = DonationStatus.Pending,
            PaymentReference = model.PaymentReference?.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        TempData["DonateOk"] = _t["mon.donate_thanks"];
        return RedirectToAction(nameof(Donate));
    }

    [HttpGet]
    public async Task<IActionResult> Membership()
    {
        ViewData["Title"] = _t["mon.membership_title"];
        var plans = await _db.SubscriptionPlans.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Price)
            .ToListAsync();

        var userId = AuthorAccess.UserId(User);
        ViewBag.HasMembership = await _membership.HasActiveMembershipAsync(userId);
        ViewBag.ActiveSub = userId is null ? null : await _membership.GetActiveSubscriptionAsync(userId);
        return View(plans);
    }

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestMembership(int planId, string? paymentReference)
    {
        var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == planId && p.IsActive);
        if (plan is null) return NotFound();

        var userId = AuthorAccess.UserId(User)!;
        _db.UserSubscriptions.Add(new UserSubscription
        {
            UserId = userId,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Pending,
            StartedAtUtc = DateTime.UtcNow,
            PaymentReference = paymentReference?.Trim(),
            Notes = "User self-request",
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        TempData["MemOk"] = _t["mon.membership_pending"];
        return RedirectToAction(nameof(Membership));
    }

    /// <summary>Tracked affiliate redirect: /go/{code}</summary>
    [HttpGet("/go/{code}")]
    public async Task<IActionResult> Go(string code)
    {
        code = (code ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(code)) return NotFound();

        var link = await _db.AffiliateLinks.FirstOrDefaultAsync(a => a.Code == code && a.IsActive);
        if (link is null) return NotFound();

        link.ClickCount++;
        link.LastClickAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Redirect(link.DestinationUrl);
    }

    [HttpGet]
    public async Task<IActionResult> AdClick(int id)
    {
        var ad = await _db.Advertisements.FirstOrDefaultAsync(a => a.Id == id && a.IsActive);
        if (ad is null) return NotFound();
        ad.ClickCount++;
        await _db.SaveChangesAsync();
        if (!string.IsNullOrWhiteSpace(ad.TargetUrl))
            return Redirect(ad.TargetUrl);
        return RedirectToAction("Index", "Home");
    }
}

public class DonateForm
{
    [Range(1, 1_000_000_000)]
    public decimal Amount { get; set; }

    [MaxLength(8)]
    public string Currency { get; set; } = "IRT";

    [MaxLength(120)]
    public string? DonorName { get; set; }

    [EmailAddress, MaxLength(200)]
    public string? DonorEmail { get; set; }

    [MaxLength(500)]
    public string? Message { get; set; }

    public bool IsAnonymous { get; set; }

    [MaxLength(200)]
    public string? PaymentReference { get; set; }
}
