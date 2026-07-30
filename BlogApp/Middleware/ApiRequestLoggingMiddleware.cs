using System.Diagnostics;
using BlogApp.Data;
using BlogApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Middleware;

public sealed class ApiRequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiRequestLoggingMiddleware> _logger;

    public ApiRequestLoggingMiddleware(RequestDelegate next, ILogger<ApiRequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db)
    {
        var path = context.Request.Path.Value ?? "";
        if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/docs", StringComparison.OrdinalIgnoreCase))
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
            try { await WriteLogAsync(context, db, sw.ElapsedMilliseconds); }
            catch (Exception ex) { _logger.LogDebug(ex, "API request log failed"); }
        }
    }

    private static async Task WriteLogAsync(HttpContext context, ApplicationDbContext db, long elapsedMs)
    {
        var user = context.User;
        int? keyId = null;
        var keyClaim = user.FindFirst("api_key_id")?.Value;
        if (int.TryParse(keyClaim, out var kid) && kid > 0) keyId = kid;

        var userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var userName = user.Identity?.Name;
        string? prefix = null;

        if (keyId is int id)
        {
            prefix = await db.ApiKeys.AsNoTracking()
                .Where(k => k.Id == id)
                .Select(k => k.KeyPrefix)
                .FirstOrDefaultAsync();
        }

        var status = context.Response.StatusCode;
        var path = context.Request.Path.Value ?? "/";
        if (path.Length > 400) path = path[..400];

        var query = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : null;
        if (query is { Length: > 200 }) query = query[..200];

        var ip = context.Connection.RemoteIpAddress?.ToString();
        var fwd = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(fwd)) ip = fwd.Split(',')[0].Trim();

        var ua = context.Request.Headers.UserAgent.FirstOrDefault();
        if (ua is { Length: > 200 }) ua = ua[..200];

        var prev = db.ChangeTracker.QueryTrackingBehavior;
        db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;

        db.ApiRequestLogs.Add(new ApiRequestLog
        {
            ApiKeyId = keyId,
            UserId = userId,
            UserName = userName,
            KeyPrefix = prefix,
            Method = context.Request.Method.Length > 10 ? context.Request.Method[..10] : context.Request.Method,
            Path = path,
            Query = query,
            StatusCode = status,
            DurationMs = (int)Math.Min(elapsedMs, int.MaxValue),
            IpAddress = ip,
            UserAgent = ua,
            IsError = status >= 400,
            IsRateLimited = status == 429,
            CreatedAtUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        db.ChangeTracker.QueryTrackingBehavior = prev;
        db.ChangeTracker.Clear();
    }
}
