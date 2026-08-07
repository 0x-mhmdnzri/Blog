using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using BlogApp.Models;
using BlogApp.Services.Seo;

namespace BlogApp.Middleware;

/// <summary>
/// Records known crawler hits (search + AI) without blocking the pipeline.
/// Skips static assets. Enqueues to <see cref="BotCrawlLogQueue"/>.
/// </summary>
public sealed class BotCrawlLoggingMiddleware
{
    private static readonly PathString Css = new("/css");
    private static readonly PathString Js = new("/js");
    private static readonly PathString Lib = new("/lib");
    private static readonly PathString Favicon = new("/favicon.ico");

    private readonly RequestDelegate _next;

    public BotCrawlLoggingMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, BotCrawlLogQueue queue)
    {
        var ua = context.Request.Headers.UserAgent.ToString();
        if (!BotDetector.TryMatch(ua, out var match) || IsStatic(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();
            try
            {
                var path = context.Request.Path.Value ?? "/";
                if (path.Length > 500) path = path[..500];

                var query = context.Request.QueryString.HasValue
                    ? context.Request.QueryString.Value
                    : null;
                if (query is { Length: > 300 }) query = query[..300];

                var uaTrim = ua.Length > 300 ? ua[..300] : ua;

                queue.TryEnqueue(new BotCrawlHit
                {
                    HitAtUtc = DateTime.UtcNow,
                    BotFamily = match.Family,
                    BotKind = match.Kind,
                    UserAgent = uaTrim,
                    Method = context.Request.Method.Length > 16
                        ? context.Request.Method[..16]
                        : context.Request.Method,
                    Path = path,
                    Query = query,
                    StatusCode = context.Response.StatusCode,
                    ElapsedMs = (int)Math.Min(sw.ElapsedMilliseconds, int.MaxValue),
                    IpHash = HashIp(context.Connection.RemoteIpAddress?.ToString())
                });
            }
            catch
            {
                // never break the response for logging
            }
        }
    }

    private static bool IsStatic(PathString path) =>
        path.StartsWithSegments(Css)
        || path.StartsWithSegments(Js)
        || path.StartsWithSegments(Lib)
        || path.StartsWithSegments(Favicon)
        || path.StartsWithSegments("/media");

    private static string? HashIp(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return null;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(ip));
        return Convert.ToHexString(hash.AsSpan(0, 8)); // 16 hex chars
    }
}
