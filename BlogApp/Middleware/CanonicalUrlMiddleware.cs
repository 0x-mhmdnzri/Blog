namespace BlogApp.Middleware;

/// <summary>
/// P1.1 — reduce crawl waste from duplicate/parameter URLs:
/// trailing slash collapse, multi-slash collapse, strip tracking query params (301).
/// </summary>
public sealed class CanonicalUrlMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>Query keys that never change content identity for public pages.</summary>
    private static readonly HashSet<string> TrackingKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "utm_source", "utm_medium", "utm_campaign", "utm_term", "utm_content", "utm_id",
        "gclid", "gbraid", "wbraid", "fbclid", "msclkid", "twclid", "ttclid",
        "mc_cid", "mc_eid", "_ga", "_gl", "ref", "ref_src", "source"
    };

    public CanonicalUrlMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? "/";
        if (IsExempt(path))
        {
            await _next(context);
            return;
        }

        var changed = false;
        var normalized = path.Replace('\\', '/');
        while (normalized.Contains("//", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
            changed = true;
        }

        if (normalized.Length > 1 && normalized.EndsWith('/'))
        {
            normalized = normalized.TrimEnd('/');
            changed = true;
        }

        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
            changed = true;
        }

        var qs = context.Request.Query;
        var kept = new List<KeyValuePair<string, string>>();
        var stripped = false;
        foreach (var kv in qs)
        {
            if (TrackingKeys.Contains(kv.Key) || kv.Key.StartsWith("utm_", StringComparison.OrdinalIgnoreCase))
            {
                stripped = true;
                continue;
            }
            foreach (var v in kv.Value)
            {
                if (v is not null)
                    kept.Add(new KeyValuePair<string, string>(kv.Key, v));
            }
        }

        if (!changed && !stripped)
        {
            await _next(context);
            return;
        }

        var target = normalized;
        if (kept.Count > 0)
        {
            var q = string.Join("&", kept.Select(p =>
                $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
            target = normalized + "?" + q;
        }

        context.Response.StatusCode = StatusCodes.Status301MovedPermanently;
        context.Response.Headers.Location = target;
        context.Response.Headers.CacheControl = "public, max-age=86400";
    }

    private static bool IsExempt(string path) =>
        path.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/Account", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/media/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/ready", StringComparison.OrdinalIgnoreCase)
        || path.Contains('.', StringComparison.Ordinal);
}
