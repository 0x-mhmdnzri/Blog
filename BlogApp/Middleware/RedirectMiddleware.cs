using BlogApp.Data;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Middleware;

/// <summary>
/// Applies active RedirectRule rows before MVC routing.
/// Location target is validated (relative path or same-host absolute) to block open redirects.
/// </summary>
public class RedirectMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RedirectMiddleware> _logger;

    public RedirectMiddleware(RequestDelegate next, ILogger<RedirectMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db)
    {
        if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
        {
            var path = context.Request.Path.Value ?? "/";
            if (!path.StartsWith('/')) path = "/" + path;

            // Never hijack auth / admin / upload
            if (!path.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase)
                && !path.StartsWith("/Account", StringComparison.OrdinalIgnoreCase)
                && !path.StartsWith("/media/", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var rule = await db.RedirectRules
                        .AsNoTracking()
                        .FirstOrDefaultAsync(r => r.IsActive && r.FromPath == path);

                    if (rule is not null)
                    {
                        if (!IsSafeRedirectTarget(context, rule.ToUrl))
                        {
                            _logger.LogWarning(
                                "Blocked unsafe redirect rule RuleId={RuleId} To={ToUrl}",
                                rule.Id, rule.ToUrl);
                        }
                        else
                        {
                            _logger.LogInformation(
                                "SEO redirect From={FromPath} To={ToUrl} Status={StatusCode} RuleId={RuleId}",
                                rule.FromPath, rule.ToUrl, rule.StatusCode, rule.Id);

                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await using var scope = context.RequestServices.CreateAsyncScope();
                                    var scopedDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                                    var tracked = await scopedDb.RedirectRules.FindAsync(rule.Id);
                                    if (tracked is not null)
                                    {
                                        tracked.HitCount++;
                                        tracked.LastHitAtUtc = DateTime.UtcNow;
                                        await scopedDb.SaveChangesAsync();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Failed to increment redirect HitCount RuleId={RuleId}", rule.Id);
                                }
                            });

                            var status = rule.StatusCode is 301 or 302 or 307 or 308 ? rule.StatusCode : 301;
                            context.Response.StatusCode = status;
                            context.Response.Headers.Location = rule.ToUrl;
                            return;
                        }
                    }
                }
                catch (Microsoft.Data.Sqlite.SqliteException ex)
                {
                    _logger.LogDebug(ex, "RedirectRules table missing — run SchemaBootstrap");
                }
            }
        }

        await _next(context);
    }

    private static bool IsSafeRedirectTarget(HttpContext context, string? toUrl)
    {
        if (string.IsNullOrWhiteSpace(toUrl)) return false;
        toUrl = toUrl.Trim();

        // Relative path on this site
        if (toUrl.StartsWith('/') && !toUrl.StartsWith("//", StringComparison.Ordinal))
            return !toUrl.Contains('\\') && !toUrl.Contains('\0');

        if (!Uri.TryCreate(toUrl, UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        // Same host only (prevents open redirect / phishing via admin-managed rules)
        var host = context.Request.Host.Host;
        return string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase);
    }
}
