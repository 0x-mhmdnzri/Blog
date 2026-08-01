using System.ComponentModel.DataAnnotations;
using AVICRM.Services;
using Microsoft.AspNetCore.Mvc;

namespace AVICRM.Controllers;

public class NewsletterController : Controller
{
    private readonly INewsletterService _nl;
    private readonly IUiTranslator _t;
    private readonly ICultureService _culture;

    public NewsletterController(INewsletterService nl, IUiTranslator t, ICultureService culture)
    {
        _nl = nl;
        _t = t;
        _culture = culture;
    }

    [HttpGet]
    public IActionResult Index()
    {
        ViewData["Title"] = _t["nl.title"];
        return View(new NewsletterSubscribeForm());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(NewsletterSubscribeForm model)
    {
        ViewData["Title"] = _t["nl.title"];
        if (!ModelState.IsValid)
            return View(model);

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var (ok, key) = await _nl.SubscribeAsync(
            model.Email,
            model.Name,
            model.LanguageCode ?? _culture.CurrentCode,
            model.Tags,
            "web",
            baseUrl);

        TempData[ok ? "NlOk" : "NlErr"] = _t[key];
        if (ok) return RedirectToAction(nameof(Index));
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Confirm(string token)
    {
        var (ok, key) = await _nl.ConfirmAsync(token);
        ViewData["Title"] = _t["nl.confirm_title"];
        ViewBag.Message = _t[key];
        ViewBag.Ok = ok;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Unsubscribe(string token)
    {
        var (ok, key) = await _nl.UnsubscribeAsync(token);
        ViewData["Title"] = _t["nl.unsub_title"];
        ViewBag.Message = _t[key];
        ViewBag.Ok = ok;
        return View();
    }
}

public class NewsletterSubscribeForm
{
    [Required, EmailAddress, MaxLength(200)]
    public string Email { get; set; } = "";

    [MaxLength(120)]
    public string? Name { get; set; }

    [MaxLength(8)]
    public string? LanguageCode { get; set; }

    [MaxLength(400)]
    public string? Tags { get; set; }
}
