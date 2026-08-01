using AVICRM.Models;
using AVICRM.Services;
using Microsoft.AspNetCore.Mvc;

namespace AVICRM.Controllers;

/// <summary>Language switcher endpoint — sets cookie and redirects back.</summary>
public class CultureController : Controller
{
    [HttpGet("/culture/{code}")]
    [HttpPost("/culture/{code}")]
    [IgnoreAntiforgeryToken]
    public IActionResult Set(string code, string? returnUrl = null)
    {
        var culture = AppCultures.Find(code);
        if (culture is null)
            return NotFound();

        Response.Cookies.Append(
            CultureService.CookieName,
            culture.Code,
            new CookieOptions
            {
                HttpOnly = false,
                IsEssential = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromDays(365),
                Path = "/"
            });

        // Prefer a localized return path
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            var cleaned = StripCulturePrefix(returnUrl);
            return LocalRedirect($"/{culture.Code}{cleaned}");
        }

        return Redirect($"/{culture.Code}/");
    }

    private static string StripCulturePrefix(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "/") return "/";
        var p = path.StartsWith('/') ? path : "/" + path;
        var segments = p.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > 0 && AppCultures.IsSupported(segments[0]))
        {
            return segments.Length > 1
                ? "/" + string.Join('/', segments.Skip(1))
                : "/";
        }
        return p;
    }
}
