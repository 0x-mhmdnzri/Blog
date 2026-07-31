using BlogApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlogApp.Controllers;

/// <summary>
/// Accessibility tooling for staff: WCAG checklist, live page checker, guidance.
/// </summary>
[Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.Author)]
public class AdminAccessibilityController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        ViewData["Title"] = "دسترسی‌پذیری";
        ViewData["UseAdminLayout"] = true;
        return View();
    }
}
