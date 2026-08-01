using AVICRM.Models;
using AVICRM.Services;

namespace AVICRM.Middleware;

/// <summary>
/// When MaintenanceMode is on, only SuperAdmin (and static assets / health) may browse the public site.
/// </summary>
public sealed class MaintenanceMiddleware
{
    private readonly RequestDelegate _next;

    public MaintenanceMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ISiteConfigService config)
    {
        var path = context.Request.Path.Value ?? "/";

        if (IsExempt(path))
        {
            await _next(context);
            return;
        }

        var on = await config.GetBoolAsync(SiteSettingKeys.MaintenanceMode);
        if (!on)
        {
            await _next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated == true
            && context.User.IsInRole(AppRoles.SuperAdmin))
        {
            await _next(context);
            return;
        }

        // Allow login so SuperAdmin can turn maintenance off
        if (path.StartsWith("/Account/Login", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/Account/Logout", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var message = await config.GetAsync(SiteSettingKeys.MaintenanceMessage)
                      ?? "سایت موقتاً در حال نگهداری است.";
        var safeMessage = System.Net.WebUtility.HtmlEncode(message);

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers["Retry-After"] = "3600";
        context.Response.ContentType = "text/html; charset=utf-8";

        // Plain string (no $""") so CSS { } braces do not trigger CS9006.
        const string htmlTemplate = """
<!DOCTYPE html>
<html lang="fa" dir="rtl">
<head>
<meta charset="utf-8"/>
<meta name="viewport" content="width=device-width,initial-scale=1"/>
<title>نگهداری سایت</title>
<style>
body{font-family:Vazirmatn,Tahoma,sans-serif;background:#0b0e14;color:#e6e8ee;display:flex;min-height:100vh;align-items:center;justify-content:center;margin:0;padding:1.5rem;text-align:center}
.box{max-width:420px}
h1{font-size:1.4rem;margin-bottom:.75rem}
p{color:#9aa3b5;line-height:1.7}
a{color:#e3b341}
</style>
</head>
<body>
<div class="box">
<h1>سایت در حال نگهداری است</h1>
<p>__MESSAGE__</p>
<p style="margin-top:1.5rem;font-size:.85rem"><a href="/Account/Login">ورود مدیر</a></p>
</div>
</body>
</html>
""";

        await context.Response.WriteAsync(htmlTemplate.Replace("__MESSAGE__", safeMessage));
    }

    private static bool IsExempt(string path)
    {
        if (path.StartsWith("/css", StringComparison.OrdinalIgnoreCase)) return true;
        if (path.StartsWith("/js", StringComparison.OrdinalIgnoreCase)) return true;
        if (path.StartsWith("/lib", StringComparison.OrdinalIgnoreCase)) return true;
        if (path.StartsWith("/favicon", StringComparison.OrdinalIgnoreCase)) return true;
        if (path.StartsWith("/media/", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
