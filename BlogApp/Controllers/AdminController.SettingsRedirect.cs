using BlogApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlogApp.Controllers;

public partial class AdminController
{
    /// <summary>Legacy route → real settings for SuperAdmin.</summary>
    [HttpGet]
    public IActionResult Settings()
    {
        if (AuthorAccess.IsSuperAdmin(User))
            return RedirectToAction("Index", "AdminSettings");

        return View("ComingSoon", new Models.ViewModels.ComingSoonViewModel
        {
            Title = "تنظیمات سایت",
            Description = "پیکربندی عمومی فقط برای SuperAdmin در دسترس است.",
            DemoFeatures =
            [
                "نام و توضیح سایت",
                "حالت نگهداری",
                "بنر اعلان سراسری",
                "پرچم‌های ویژگی"
            ]
        });
    }
}
