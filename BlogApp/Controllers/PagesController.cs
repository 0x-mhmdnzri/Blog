using BlogApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace BlogApp.Controllers;

/// <summary>
/// Public marketing / persona pages for clients and visitors.
/// </summary>
public class PagesController : Controller
{
    private readonly SeoService _seo;
    private readonly IUiTranslator _t;

    public PagesController(SeoService seo, IUiTranslator t)
    {
        _seo = seo;
        _t = t;
    }

    [HttpGet("about")]
    public IActionResult About()
    {
        ViewData["Title"] = _t["mkt.about.title"];
        ViewData["Description"] = _t["mkt.about.desc"];
        ViewData["OgType"] = "profile";
        return View();
    }

    [HttpGet("services")]
    public IActionResult Services()
    {
        ViewData["Title"] = _t["mkt.svc.title"];
        ViewData["Description"] = _t["mkt.svc.desc"];
        return View();
    }

    [HttpGet("projects")]
    public IActionResult Projects()
    {
        ViewData["Title"] = _t["mkt.prj.title"];
        ViewData["Description"] = _t["mkt.prj.desc"];
        return View();
    }

    [HttpGet("contact")]
    public IActionResult Contact()
    {
        ViewData["Title"] = _t["mkt.ctc.title"];
        ViewData["Description"] = _t["mkt.ctc.desc"];
        return View();
    }
}
