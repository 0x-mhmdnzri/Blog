using AVICRM.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AVICRM.Controllers;

public partial class AdminController
{
    /// <summary>Placeholder for FEATURES.md CRM modules not yet implemented.</summary>
    [HttpGet]
    [Authorize]
    public IActionResult ComingSoon(string? feature = null)
    {
        var title = string.IsNullOrWhiteSpace(feature) ? "به‌زودی" : feature;
        return View(new ComingSoonViewModel
        {
            Title = title,
            Description = "این ماژول در نقشه راه FEATURES.md تعریف شده و به‌زودی پیاده‌سازی می‌شود.",
            DemoFeatures = new List<string>
            {
                "CRUD کامل و جستجو",
                "دسترسی مبتنی بر نقش",
                "گزارش و تایم‌لاین"
            }
        });
    }
}
