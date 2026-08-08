namespace BlogApp.Middleware;

/// <summary>
/// Defense-in-depth HTTP response headers (OWASP / modern browser baselines).
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    // CSP: allow self + known CDNs (Bootstrap, Chart.js, jsvectormap, Google Fonts).
    // script-src-elem is explicit so external map scripts are not forced to default-src.
    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'; " +
        "object-src 'none'; " +
        "img-src 'self' data: blob: https:; " +
        "media-src 'self' blob:; " +
        "font-src 'self' https://fonts.gstatic.com data:; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://cdn.jsdelivr.net; " +
        "script-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com https://cdn.jsdelivr.net; " +
        "script-src-elem 'self' 'unsafe-inline' https://cdnjs.cloudflare.com https://cdn.jsdelivr.net; " +
        "connect-src 'self'; " +
        "upgrade-insecure-requests";

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        var isOg = path.StartsWith("/og/", StringComparison.OrdinalIgnoreCase)
                   || path.Equals("/og", StringComparison.OrdinalIgnoreCase);

        context.Response.OnStarting(() =>
        {
            var h = context.Response.Headers;

            h["X-Content-Type-Options"] = "nosniff";
            // OG images must be embeddable by LinkedIn / WhatsApp / Telegram / X crawlers
            if (isOg)
            {
                h.Remove("X-Frame-Options");
                h["Cross-Origin-Resource-Policy"] = "cross-origin";
                h["Cross-Origin-Opener-Policy"] = "same-origin-allow-popups";
            }
            else
            {
                h["X-Frame-Options"] = "DENY";
                h["Cross-Origin-Opener-Policy"] = "same-origin";
                h["Cross-Origin-Resource-Policy"] = "same-origin";
            }

            h["Referrer-Policy"] = "strict-origin-when-cross-origin";
            h["Permissions-Policy"] =
                "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";
            h["X-Permitted-Cross-Domain-Policies"] = "none";

            if (!h.ContainsKey("Content-Security-Policy"))
                h["Content-Security-Policy"] = ContentSecurityPolicy;

            h.Remove("Server");
            h.Remove("X-Powered-By");

            return Task.CompletedTask;
        });

        await _next(context);
    }
}
