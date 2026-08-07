using System.Diagnostics;

namespace BlogApp.Middleware;

/// <summary>
/// Emits <c>Server-Timing: app;dur=N</c> so RUM / crawl tooling can measure TTFB.
/// Cheap: one Stopwatch per request, no I/O.
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
                context.Response.Headers["Server-Timing"] =
                    $"app;desc=\"BlogApp\";dur={sw.Elapsed.TotalMilliseconds:0.0}";
            }
            return Task.CompletedTask;
        });

        await _next(context);
    }
}
