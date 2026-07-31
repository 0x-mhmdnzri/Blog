using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BlogApp.Controllers;

public partial class AccountController
{
    [HttpPost, ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    [AllowAnonymous]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null)
    {
        returnUrl = SanitizeReturnUrl(returnUrl) ?? "/";
        provider = (provider ?? string.Empty).Trim();
        // Only allow schemes that were registered (keys present in config)
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(_config["Authentication:Google:ClientId"])
            && !string.IsNullOrWhiteSpace(_config["Authentication:Google:ClientSecret"]))
            allowed.Add("Google");
        if (!string.IsNullOrWhiteSpace(_config["Authentication:GitHub:ClientId"])
            && !string.IsNullOrWhiteSpace(_config["Authentication:GitHub:ClientSecret"]))
            allowed.Add("GitHub");

        if (string.IsNullOrEmpty(provider) || !allowed.Contains(provider))
        {
            ModelState.AddModelError(string.Empty, "ارائه‌دهنده ورود اجتماعی فعال نیست یا نامعتبر است.");
            ViewBag.GoogleLoginEnabled = allowed.Contains("Google");
            ViewBag.GitHubLoginEnabled = allowed.Contains("GitHub");
            return View("Login");
        }

        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    [HttpGet]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
    {
        returnUrl = SanitizeReturnUrl(returnUrl) ?? "/";
        if (!string.IsNullOrEmpty(remoteError))
        {
            ModelState.AddModelError(string.Empty, $"خطای ورود خارجی: {remoteError}");
            return View("Login");
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info is null)
        {
            ModelState.AddModelError(string.Empty, "ورود اجتماعی کامل نشد. کلیدهای OAuth را بررسی کنید.");
            return View("Login");
        }

        var result = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);

        if (result.Succeeded)
            return Redirect(returnUrl);

        // Auto-provision reader if new external identity
        var email = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                    ?? info.Principal.FindFirst("email")?.Value;
        var name = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                   ?? email?.Split('@')[0]
                   ?? "user";

        if (string.IsNullOrEmpty(email))
        {
            ModelState.AddModelError(string.Empty, "ارائه‌دهنده ایمیل برنگرداند.");
            return View("Login");
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new Models.ApplicationUser
            {
                UserName = await MakeUniqueUserNameAsync(name),
                Email = email,
                EmailConfirmed = true,
                DisplayName = name
            };
            var create = await _userManager.CreateAsync(user);
            if (!create.Succeeded)
            {
                foreach (var e in create.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);
                return View("Login");
            }
            await _userManager.AddToRoleAsync(user, Models.AppRoles.Reader);
        }

        await _userManager.AddLoginAsync(user, info);
        await _signInManager.SignInAsync(user, isPersistent: true);
        return Redirect(returnUrl);
    }

    private async Task<string> MakeUniqueUserNameAsync(string baseName)
    {
        var cleaned = new string((baseName ?? "user").Where(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-').ToArray());
        if (string.IsNullOrWhiteSpace(cleaned)) cleaned = "user";
        if (cleaned.Length > 24) cleaned = cleaned[..24];
        var candidate = cleaned;
        var i = 0;
        while (await _userManager.FindByNameAsync(candidate) is not null)
        {
            i++;
            candidate = $"{cleaned}{i}";
        }
        return candidate;
    }
}
