using Microsoft.AspNetCore.Mvc;

namespace BlogApp.Controllers;

/// <summary>Public developer handbook (human-readable guide, not machine JSON).</summary>
public class DevelopersController : Controller
{
    [HttpGet("/developers")]
    [HttpGet("/developers/api")]
    [HttpGet("/fa/developers")]
    [HttpGet("/en/developers")]
    public IActionResult Index()
    {
        ViewData["Title"] = "راهنمای توسعه‌دهندگان API";
        ViewData["Description"] =
            "راهنمای کامل کلید API (PAT)، احراز هویت، REST، GraphQL، Webhook، RSS و محدودیت نرخ در بلاگ محمد نظری.";
        ViewData["NoIndex"] = false;
        return View();
    }
}
