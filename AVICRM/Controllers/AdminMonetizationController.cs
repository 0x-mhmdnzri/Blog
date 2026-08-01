using AVICRM.Data;
using AVICRM.Models;
using AVICRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AVICRM.Controllers;

[Authorize(Roles = AppRoles.SuperAdmin)]
public class AdminMonetizationController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IUiTranslator _t;

    public AdminMonetizationController(ApplicationDbContext db, IUiTranslator t)
    {
        _db = db;
        _t = t;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? tab = null)
    {
        ViewData["Title"] = _t["admin.nav.monetization"];
        ViewBag.ActiveTab = tab ?? "overview";

        var now = DateTime.UtcNow;
        ViewBag.PlanCount = await _db.SubscriptionPlans.CountAsync();
        ViewBag.ActiveMembers = await _db.UserSubscriptions.CountAsync(s =>
            s.Status == SubscriptionStatus.Active && (s.EndsAtUtc == null || s.EndsAtUtc > now));
        ViewBag.PendingDonations = await _db.Donations.CountAsync(d => d.Status == DonationStatus.Pending);
        ViewBag.ConfirmedDonationSum = await _db.Donations
            .Where(d => d.Status == DonationStatus.Confirmed)
            .SumAsync(d => (decimal?)d.Amount) ?? 0;
        ViewBag.AdCount = await _db.Advertisements.CountAsync(a => a.IsActive);
        ViewBag.AffiliateClicks = await _db.AffiliateLinks.SumAsync(a => (int?)a.ClickCount) ?? 0;
        ViewBag.PremiumPosts = await _db.Posts.CountAsync(p => p.IsPremium && !p.IsDeleted);

        ViewBag.Plans = await _db.SubscriptionPlans.OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToListAsync();
        ViewBag.Subscriptions = await _db.UserSubscriptions.Include(s => s.Plan)
            .OrderByDescending(s => s.CreatedAtUtc).Take(50).ToListAsync();
        ViewBag.Donations = await _db.Donations.OrderByDescending(d => d.CreatedAtUtc).Take(50).ToListAsync();
        ViewBag.Ads = await _db.Advertisements.OrderBy(a => a.SortOrder).ThenByDescending(a => a.Id).ToListAsync();
        ViewBag.Affiliates = await _db.AffiliateLinks.OrderByDescending(a => a.ClickCount).ToListAsync();

        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePlan(int? id, string name, string code, string? description,
        decimal price, string currency, int durationDays, bool isActive, int sortOrder)
    {
        code = (code ?? "").Trim().ToLowerInvariant();
        name = (name ?? "").Trim();
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(code))
        {
            TempData["MonErr"] = _t["mon.err_plan"];
            return RedirectToAction(nameof(Index), new { tab = "plans" });
        }

        SubscriptionPlan plan;
        if (id is > 0)
        {
            plan = await _db.SubscriptionPlans.FindAsync(id) ?? new SubscriptionPlan();
            if (plan.Id == 0) _db.SubscriptionPlans.Add(plan);
        }
        else
        {
            plan = new SubscriptionPlan { CreatedAtUtc = DateTime.UtcNow };
            _db.SubscriptionPlans.Add(plan);
        }

        plan.Name = name;
        plan.Code = code;
        plan.Description = description?.Trim();
        plan.Price = price;
        plan.Currency = string.IsNullOrWhiteSpace(currency) ? "IRT" : currency.Trim().ToUpperInvariant();
        plan.DurationDays = durationDays < 0 ? 0 : durationDays;
        plan.IsActive = isActive;
        plan.SortOrder = sortOrder;

        await _db.SaveChangesAsync();
        TempData["MonOk"] = _t["mon.saved_plan"];
        return RedirectToAction(nameof(Index), new { tab = "plans" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePlan(int id)
    {
        var plan = await _db.SubscriptionPlans.FindAsync(id);
        if (plan is not null)
        {
            _db.SubscriptionPlans.Remove(plan);
            await _db.SaveChangesAsync();
            TempData["MonOk"] = _t["mon.deleted"];
        }
        return RedirectToAction(nameof(Index), new { tab = "plans" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GrantMembership(string userId, int planId, string? paymentReference, string? notes)
    {
        userId = (userId ?? "").Trim();
        if (string.IsNullOrEmpty(userId))
        {
            TempData["MonErr"] = _t["mon.err_user"];
            return RedirectToAction(nameof(Index), new { tab = "members" });
        }

        var plan = await _db.SubscriptionPlans.FindAsync(planId);
        if (plan is null)
        {
            TempData["MonErr"] = _t["mon.err_plan"];
            return RedirectToAction(nameof(Index), new { tab = "members" });
        }

        var ends = plan.DurationDays <= 0 ? (DateTime?)null : DateTime.UtcNow.AddDays(plan.DurationDays);
        _db.UserSubscriptions.Add(new UserSubscription
        {
            UserId = userId,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
            StartedAtUtc = DateTime.UtcNow,
            EndsAtUtc = ends,
            PaymentReference = paymentReference?.Trim(),
            Notes = notes?.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        TempData["MonOk"] = _t["mon.granted"];
        return RedirectToAction(nameof(Index), new { tab = "members" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelMembership(int id)
    {
        var sub = await _db.UserSubscriptions.FindAsync(id);
        if (sub is not null)
        {
            sub.Status = SubscriptionStatus.Cancelled;
            await _db.SaveChangesAsync();
            TempData["MonOk"] = _t["mon.cancelled"];
        }
        return RedirectToAction(nameof(Index), new { tab = "members" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmDonation(int id)
    {
        var d = await _db.Donations.FindAsync(id);
        if (d is not null)
        {
            d.Status = DonationStatus.Confirmed;
            d.ConfirmedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            TempData["MonOk"] = _t["mon.donation_ok"];
        }
        return RedirectToAction(nameof(Index), new { tab = "donations" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectDonation(int id)
    {
        var d = await _db.Donations.FindAsync(id);
        if (d is not null)
        {
            d.Status = DonationStatus.Rejected;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index), new { tab = "donations" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAd(int? id, string name, AdPlacement placement, string htmlContent,
        string? targetUrl, bool isActive, int sortOrder)
    {
        name = (name ?? "").Trim();
        if (string.IsNullOrEmpty(name))
        {
            TempData["MonErr"] = _t["mon.err_ad"];
            return RedirectToAction(nameof(Index), new { tab = "ads" });
        }

        Advertisement ad;
        if (id is > 0)
            ad = await _db.Advertisements.FindAsync(id) ?? new Advertisement();
        else
        {
            ad = new Advertisement { CreatedAtUtc = DateTime.UtcNow };
            _db.Advertisements.Add(ad);
        }

        if (ad.Id == 0 && id is > 0) _db.Advertisements.Add(ad);

        ad.Name = name;
        ad.Placement = placement;
        ad.HtmlContent = htmlContent ?? "";
        ad.TargetUrl = targetUrl?.Trim();
        ad.IsActive = isActive;
        ad.SortOrder = sortOrder;

        await _db.SaveChangesAsync();
        TempData["MonOk"] = _t["mon.saved_ad"];
        return RedirectToAction(nameof(Index), new { tab = "ads" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAd(int id)
    {
        var ad = await _db.Advertisements.FindAsync(id);
        if (ad is not null)
        {
            _db.Advertisements.Remove(ad);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index), new { tab = "ads" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAffiliate(int? id, string code, string title, string destinationUrl,
        string? network, bool isActive)
    {
        code = (code ?? "").Trim().ToLowerInvariant();
        title = (title ?? "").Trim();
        destinationUrl = (destinationUrl ?? "").Trim();
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(title) || string.IsNullOrEmpty(destinationUrl))
        {
            TempData["MonErr"] = _t["mon.err_aff"];
            return RedirectToAction(nameof(Index), new { tab = "affiliates" });
        }

        if (!Uri.TryCreate(destinationUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            TempData["MonErr"] = _t["mon.err_url"];
            return RedirectToAction(nameof(Index), new { tab = "affiliates" });
        }

        AffiliateLink link;
        if (id is > 0)
            link = await _db.AffiliateLinks.FindAsync(id) ?? new AffiliateLink();
        else
        {
            link = new AffiliateLink { CreatedAtUtc = DateTime.UtcNow };
            _db.AffiliateLinks.Add(link);
        }

        if (link.Id == 0 && id is > 0) _db.AffiliateLinks.Add(link);

        link.Code = code;
        link.Title = title;
        link.DestinationUrl = destinationUrl;
        link.Network = network?.Trim();
        link.IsActive = isActive;

        await _db.SaveChangesAsync();
        TempData["MonOk"] = _t["mon.saved_aff"];
        return RedirectToAction(nameof(Index), new { tab = "affiliates" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAffiliate(int id)
    {
        var link = await _db.AffiliateLinks.FindAsync(id);
        if (link is not null)
        {
            _db.AffiliateLinks.Remove(link);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index), new { tab = "affiliates" });
    }
}
