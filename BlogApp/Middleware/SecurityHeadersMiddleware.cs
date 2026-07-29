namespace BlogApp.Middleware;

/// <summary>
/// Defense-in-depth HTTP response headers (OWASP / modern browser baselines).
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    // CSP: allow self + known CDNs used by the public layout (Bootstrap, hljs, Google Fonts).
    // Tighten further when offline-only assets are fully local.
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
        "connect-src 'self'; " +
        "upgrade-insecure-requests";

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var h = context.Response.Headers;

            h["X-Content-Type-Options"] = "nosniff";
            h["X-Frame-Options"] = "DENY";
            h["Referrer-Policy"] = "strict-origin-when-cross-origin";
            h["Permissions-Policy"] =
                "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";
            h["Cross-Origin-Opener-Policy"] = "same-origin";
            h["Cross-Origin-Resource-Policy"] = "same-origin";
            h["X-Permitted-Cross-Domain-Policies"] = "none";

            // Do not overwrite if a more specific CSP was set by an action.
            if (!h.ContainsKey("Content-Security-Policy"))
                h["Content-Security-Policy"] = ContentSecurityPolicy;

            // Strip legacy server fingerprints if any proxy added them.
            h.Remove("Server");
            h.Remove("X-Powered-By");

            return Task.CompletedTask;
        });

        await _next(context);
    }
}
