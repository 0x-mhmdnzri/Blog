using System.Diagnostics;
using System.Globalization;

namespace BlogApp.Middleware;

/// <summary>
/// Emits <c>Server-Timing: app;dur=N</c> so RUM / crawl tooling can measure TTFB.
/// Cheap: one Stopwatch per request, no I/O.
/// Always formats the duration with InvariantCulture so fa-IR / ar
/// never inject U+066B (Arabic decimal separator) into the header value
/// (Kestrel rejects non-ASCII in headers → ObjectDisposedException).
/// </summary>
public sealed class ServerTimingMiddleware
{
    private readonly RequestDelegate _next;

    public ServerTimingMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        context.Response.OnStarting(() =>
        {
            sw.Stop();
            // Avoid overwriting if already set by a later layer
            if (!context.Response.Headers.ContainsKey("Server-Timing"))
            {
                var dur = sw.Elapsed.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture);
                context.Response.Headers["Server-Timing"] =
                    $"app;desc=\"BlogApp\";dur={dur}";
            }
            return Task.CompletedTask;
        });

        await _next(context);
    }
}
