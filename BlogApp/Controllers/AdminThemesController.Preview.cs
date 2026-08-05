using BlogApp.Models;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public partial class AdminThemesController
{
    /// <summary>
    /// SuperAdmin only: apply theme on this browser (cookie) so they can review
    /// contrasts and UI on the real site before approve/reject. Does not activate site-wide.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Preview(int id, string? returnUrl = null)
    {
        var t = await _db.CustomThemes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();

        Response.Cookies.Append(ThemesController.PreviewCookie, t.Id.ToString(), new CookieOptions
        {
            Path = "/",
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            MaxAge = TimeSpan.FromHours(4),
            Expires = DateTimeOffset.UtcNow.AddHours(4)
        });

        await _audit.LogAsync("theme.preview", "CustomTheme", id.ToString(), t.Name, HttpContext);
        TempData["Msg"] = $"پیش‌نمایش «{t.Name}» فعال شد — سایت را با این تم ببینید، سپس تأیید یا رد کنید.";

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return Redirect("~/");
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public IActionResult EndPreview(string? returnUrl = null)
    {
        Response.Cookies.Delete(ThemesController.PreviewCookie, new CookieOptions { Path = "/" });
        TempData["Msg"] = "پیش‌نمایش تم پایان یافت — تم فعال سایت بازگردانده شد.";
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction(nameof(Index));
    }
}
