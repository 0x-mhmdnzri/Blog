using BlogApp.Data;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Middleware;

/// <summary>
/// Applies active RedirectRule rows before MVC routing.
/// P1.1: flattens redirect chains to a single hop (max 5) so crawlers burn less budget.
/// Location target is validated (relative path or same-host absolute) to block open redirects.
/// </summary>
public class RedirectMiddleware
{
    private const int MaxChainHops = 5;

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

            if (!path.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase)
                && !path.StartsWith("/Account", StringComparison.OrdinalIgnoreCase)
                && !path.StartsWith("/media/", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var start = await db.RedirectRules.AsNoTracking()
                        .Where(r => r.IsActive && r.FromPath == path)
                        .Select(r => new RuleRow(r.Id, r.FromPath, r.ToUrl, r.StatusCode))
                        .FirstOrDefaultAsync();

                    if (start is not null)
                    {
                        var byFrom = new Dictionary<string, RuleRow>(StringComparer.OrdinalIgnoreCase)
                        {
                            [start.FromPath] = start
                        };

                        var more = await db.RedirectRules.AsNoTracking()
                            .Where(r => r.IsActive && r.FromPath != path)
                            .Select(r => new RuleRow(r.Id, r.FromPath, r.ToUrl, r.StatusCode))
                            .ToListAsync();
                        foreach (var r in more)
                        {
                            if (!byFrom.ContainsKey(r.FromPath))
                                byFrom[r.FromPath] = r;
                        }

                        var (finalUrl, status, hops) = ResolveChain(byFrom, path, start);

                        if (!IsSafeRedirectTarget(context, finalUrl))
                        {
                            _logger.LogWarning(
                                "Blocked unsafe redirect rule RuleId={RuleId} To={ToUrl}",
                                start.Id, finalUrl);
                        }
                        else
                        {
                            if (hops > 1)
                                _logger.LogInformation(
                                    "SEO redirect chain flattened hops={Hops} From={FromPath} To={ToUrl} RuleId={RuleId}",
                                    hops, path, finalUrl, start.Id);
                            else
                                _logger.LogInformation(
                                    "SEO redirect From={FromPath} To={ToUrl} Status={StatusCode} RuleId={RuleId}",
                                    path, finalUrl, status, start.Id);

                            var ruleId = start.Id;
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await using var scope = context.RequestServices.CreateAsyncScope();
                                    var scopedDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                                    var tracked = await scopedDb.RedirectRules.FindAsync(ruleId);
                                    if (tracked is not null)
                                    {
                                        tracked.HitCount++;
                                        tracked.LastHitAtUtc = DateTime.UtcNow;
                                        await scopedDb.SaveChangesAsync();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Failed to increment redirect HitCount RuleId={RuleId}", ruleId);
                                }
                            });

                            var code = status is 301 or 302 or 307 or 308 ? status : 301;
                            context.Response.StatusCode = code;
                            context.Response.Headers.Location = finalUrl;
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

    private sealed record RuleRow(int Id, string FromPath, string ToUrl, int StatusCode);

    private static (string FinalUrl, int Status, int Hops) ResolveChain(
        Dictionary<string, RuleRow> byFrom,
        string startPath,
        RuleRow start)
    {
        var current = start.ToUrl.Trim();
        var status = start.StatusCode is 301 or 302 or 307 or 308 ? start.StatusCode : 301;
        var hops = 1;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { startPath };

        for (var i = 0; i < MaxChainHops; i++)
        {
            var nextPath = ToRelativePath(current);
            if (nextPath is null || !seen.Add(nextPath))
                break;

            if (!byFrom.TryGetValue(nextPath, out var next))
                break;

            current = next.ToUrl.Trim();
            if (next.StatusCode is 302 or 307)
                status = next.StatusCode;
            hops++;
        }

        return (current, status, hops);
    }

    private static string? ToRelativePath(string toUrl)
    {
        if (string.IsNullOrWhiteSpace(toUrl)) return null;
        toUrl = toUrl.Trim();
        if (toUrl.StartsWith('/') && !toUrl.StartsWith("//", StringComparison.Ordinal))
        {
            var q = toUrl.IndexOf('?', StringComparison.Ordinal);
            return q >= 0 ? toUrl[..q] : toUrl;
        }
        if (Uri.TryCreate(toUrl, UriKind.Absolute, out var uri))
        {
            var p = uri.AbsolutePath;
            return string.IsNullOrEmpty(p) ? "/" : p;
        }
        return null;
    }

    private static bool IsSafeRedirectTarget(HttpContext context, string? toUrl)
    {
        if (string.IsNullOrWhiteSpace(toUrl)) return false;
        toUrl = toUrl.Trim();

        if (toUrl.StartsWith('/') && !toUrl.StartsWith("//", StringComparison.Ordinal))
            return !toUrl.Contains('\\') && !toUrl.Contains('\0');

        if (!Uri.TryCreate(toUrl, UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        var host = context.Request.Host.Host;
        return string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase);
    }
}
