using System.Diagnostics;
using BlogApp.Logging;
using Serilog.Context;

namespace BlogApp.Middleware;

/// <summary>
/// Attaches a correlation id to every request and writes one structured summary log
/// (method, path, status, elapsed ms, user). Skips static assets to stay cheap.
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private static readonly PathString Css = new("/css");
    private static readonly PathString Js = new("/js");
    private static readonly PathString Lib = new("/lib");
    private static readonly PathString Favicon = new("/favicon.ico");

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsStatic(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var correlationId = context.Request.Headers[SerilogBootstrap.CorrelationHeader].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId))
            correlationId = Guid.NewGuid().ToString("N");

        context.TraceIdentifier = correlationId;
        context.Response.Headers[SerilogBootstrap.CorrelationHeader] = correlationId;

        var sw = Stopwatch.StartNew();
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestPath", context.Request.Path.Value))
        using (LogContext.PushProperty("RequestMethod", context.Request.Method))
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unhandled exception {Method} {Path} after {ElapsedMs}ms",
                    context.Request.Method,
                    context.Request.Path.Value,
                    sw.ElapsedMilliseconds);
                throw;
            }
            finally
            {
                sw.Stop();
                var status = context.Response.StatusCode;
                var user = context.User?.Identity?.IsAuthenticated == true
                    ? context.User.Identity!.Name
                    : null;

                if (status >= 500)
                {
                    _logger.LogError(
                        "HTTP {StatusCode} {Method} {Path} in {ElapsedMs}ms User={User}",
                        status, context.Request.Method, context.Request.Path.Value, sw.ElapsedMilliseconds, user);
                }
                else if (status >= 400)
                {
                    _logger.LogWarning(
                        "HTTP {StatusCode} {Method} {Path} in {ElapsedMs}ms User={User}",
                        status, context.Request.Method, context.Request.Path.Value, sw.ElapsedMilliseconds, user);
                }
                else
                {
                    _logger.LogInformation(
                        "HTTP {StatusCode} {Method} {Path} in {ElapsedMs}ms User={User}",
                        status, context.Request.Method, context.Request.Path.Value, sw.ElapsedMilliseconds, user);
                }
            }
        }
    }

    private static bool IsStatic(PathString path) =>
        path.StartsWithSegments(Css)
        || path.StartsWithSegments(Js)
        || path.StartsWithSegments(Lib)
        || path.StartsWithSegments(Favicon);
}
