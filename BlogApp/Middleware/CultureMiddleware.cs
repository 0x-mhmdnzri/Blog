using BlogApp.Models;
using BlogApp.Services;

namespace BlogApp.Middleware;

/// <summary>
/// Resolves culture from path prefix (/en/..., /fa/..., /ar/...), cookie, Accept-Language, or default.
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
        CultureDescriptor culture;
        string? pathCulture = null;

        // Path: /{culture}/... or /{culture}
        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > 0)
        {
            var candidate = AppCultures.Find(segments[0]);
            if (candidate is not null)
            {
                pathCulture = candidate.Code;
                culture = candidate;
                // Rewrite path without culture prefix so existing routes keep working
                var rest = segments.Length > 1
                    ? "/" + string.Join('/', segments.Skip(1))
                    : "/";
                context.Request.Path = rest;
                // Preserve original path for link generation helpers
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

        // Persist preference
        if (pathCulture is not null
            || context.Request.Cookies[CultureService.CookieName] != culture.Code)
        {
            context.Response.Cookies.Append(
                CultureService.CookieName,
                culture.Code,
                new CookieOptions
                {
                    HttpOnly = false,
                    IsEssential = true,
                    Secure = context.Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    MaxAge = TimeSpan.FromDays(365),
                    Path = "/"
                });
        }

        var cultureInfo = new System.Globalization.CultureInfo(culture.Locale);
        System.Globalization.CultureInfo.CurrentCulture = cultureInfo;
        System.Globalization.CultureInfo.CurrentUICulture = cultureInfo;

        await _next(context);
    }

    private static CultureDescriptor ResolveFromCookieOrHeader(HttpContext context)
    {
        var cookie = context.Request.Cookies[CultureService.CookieName];
        var fromCookie = AppCultures.Find(cookie);
        if (fromCookie is not null) return fromCookie;

        var accept = context.Request.GetTypedHeaders().AcceptLanguage;
        if (accept is { Count: > 0 })
        {
            foreach (var lang in accept.OrderByDescending(x => x.Quality ?? 1))
            {
                var match = AppCultures.Find(lang.Value.Value);
                if (match is not null) return match;
            }
        }

        return AppCultures.Find(AppCultures.Default)!;
    }
}
