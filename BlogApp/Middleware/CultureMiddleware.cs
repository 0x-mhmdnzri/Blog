using BlogApp.Models;
using BlogApp.Services;

namespace BlogApp.Middleware;

/// <summary>
/// Resolves culture from path prefix (/en/..., /fa/..., /ar/...), then cookie, then site default (FA).
/// Cookie is ALWAYS refreshed so the user's choice survives across pages and sessions (1 year sliding).
/// Sets CultureInfo and stores CultureDescriptor in HttpContext.Items.
/// Strips culture segment from Path for downstream routing when present.
/// </summary>
public sealed class CultureMiddleware
{
    private readonly RequestDelegate _next;

    public CultureMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";
        if (IsStaticAsset(path))
        {
            await _next(context);
            return;
        }

        CultureDescriptor culture;

        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > 0)
        {
            var candidate = AppCultures.Find(segments[0]);
            if (candidate is not null)
            {
                culture = candidate;
                var rest = segments.Length > 1
                    ? "/" + string.Join('/', segments.Skip(1))
                    : "/";
                context.Request.Path = rest;
                context.Items["OriginalPath"] = path;
            }
            else
            {
                culture = ResolveFromCookieOrHeader(context);
            }
        }
        else
        {
            culture = ResolveFromCookieOrHeader(context);
        }

        context.Items[CultureService.HttpContextKey] = culture;
        WriteCultureCookie(context, culture.Code);

        var cultureInfo = new System.Globalization.CultureInfo(culture.Locale);
        System.Globalization.CultureInfo.CurrentCulture = cultureInfo;
        System.Globalization.CultureInfo.CurrentUICulture = cultureInfo;

        await _next(context);
    }

    private static void WriteCultureCookie(HttpContext context, string code)
    {
        if (context.Response.HasStarted) return;

        context.Response.Cookies.Append(
            CultureService.CookieName,
            code,
            new CookieOptions
            {
                HttpOnly = false,
                IsEssential = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromDays(365),
                Expires = DateTimeOffset.UtcNow.AddDays(365),
                Path = "/"
            });
    }

    /// <summary>
    /// Cookie always wins when present. Without a cookie the site default is FA (Persian).
    /// Accept-Language is intentionally ignored so browsers set to English do not flip the UI.
    /// </summary>
    private static CultureDescriptor ResolveFromCookieOrHeader(HttpContext context)
    {
        var cookie = context.Request.Cookies[CultureService.CookieName];
        var fromCookie = AppCultures.Find(cookie);
        if (fromCookie is not null) return fromCookie;

        return AppCultures.Find(AppCultures.Default)!;
    }

    private static bool IsStaticAsset(string path)
    {
        var p = path.AsSpan();
        return p.StartsWith("/css/", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("/js/", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("/images/", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("/favicon", StringComparison.OrdinalIgnoreCase)
            || p.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
            || p.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
            || p.EndsWith(".map", StringComparison.OrdinalIgnoreCase)
            || p.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase)
            || p.EndsWith(".woff", StringComparison.OrdinalIgnoreCase)
            || p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || p.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            || p.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
            || p.EndsWith(".ico", StringComparison.OrdinalIgnoreCase);
    }
}
