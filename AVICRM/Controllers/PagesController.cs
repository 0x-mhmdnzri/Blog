using AVICRM.Services;
using Microsoft.AspNetCore.Mvc;

namespace AVICRM.Controllers;

/// <summary>
/// Public marketing / persona pages for clients and visitors.
/// </summary>
public class PagesController : Controller
{
    private readonly SeoService _seo;

    public PagesController(SeoService seo) => _seo = seo;

    [HttpGet("about")]
    public IActionResult About()
    {
        ViewData["Title"] = "درباره من";
        ViewData["Description"] = "محمد نظری — Senior .NET Backend Engineer؛ Clean Architecture، DDD، CQRS و سیستم‌های توزیع‌شده.";
        ViewData["OgType"] = "profile";
        return View();
    }

    [HttpGet("services")]
    public IActionResult Services()
    {
        ViewData["Title"] = "خدمات";
        ViewData["Description"] = "طراحی و پیاده‌سازی بک‌اند .NET، معماری میکروسرویس، پرداخت، ERP و زیرساخت پیام‌رسانی.";
        return View();
    }

    [HttpGet("projects")]
    public IActionResult Projects()
    {
        ViewData["Title"] = "پروژه‌ها";
        ViewData["Description"] = "نمونه پروژه‌ها: Artix.API، Wallet، IDPServer، RabbitMQ patterns و File-Uploader.";
        return View();
    }

    [HttpGet("contact")]
    public IActionResult Contact()
    {
        ViewData["Title"] = "تماس";
        ViewData["Description"] = "ارتباط با محمد نظری برای همکاری، مشاوره معماری و پروژه‌های .NET.";
        return View();
    }
}
