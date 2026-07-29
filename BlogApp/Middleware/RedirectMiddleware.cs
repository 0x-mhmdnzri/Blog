using BlogApp.Data;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Middleware;

/// <summary>
/// Applies active RedirectRule rows before MVC routing. Exact path match only
/// (query string ignored for matching). Increments HitCount asynchronously.
/// </summary>
public class RedirectMiddleware
{
    private readonly RequestDelegate _next;

    public RedirectMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db)
    {
        // Only intercept GET/HEAD for public-looking paths
        if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
        {
            var path = context.Request.Path.Value ?? "/";
            if (!path.StartsWith('/')) path = "/" + path;

            // Skip admin/api noise
            if (!path.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase)
                && !path.StartsWith("/Account", StringComparison.OrdinalIgnoreCase)
                && !path.StartsWith("/media/upload", StringComparison.OrdinalIgnoreCase))
            {
                var rule = await db.RedirectRules
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.IsActive && r.FromPath == path);

                if (rule is not null)
                {
                    // Fire-and-forget hit counter (best-effort)
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
                        catch { /* ignore */ }
                    });

                    var status = rule.StatusCode is 301 or 302 or 307 or 308 ? rule.StatusCode : 301;
                    context.Response.StatusCode = status;
                    context.Response.Headers.Location = rule.ToUrl;
                    return;
                }
            }
        }

        await _next(context);
    }
}
