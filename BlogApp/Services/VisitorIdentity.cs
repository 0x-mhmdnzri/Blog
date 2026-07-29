using System.Security.Cryptography;
using System.Text;

namespace BlogApp.Services;

/// <summary>
/// Builds a one-way fingerprint from a visitor's IP address + User-Agent string, used to
/// tell "the same visitor reloading the page" apart from "a genuinely new view" without
/// storing the raw IP or User-Agent anywhere. The raw values only ever exist in memory for
/// the duration of this call.
/// </summary>
public static class VisitorIdentity
{
    public static string ComputeHash(HttpContext context)
    {
        var ip = ResolveClientIp(context);
        var userAgent = context.Request.Headers["User-Agent"].ToString();

        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes($"{ip}|{userAgent}"));
        return Convert.ToHexString(bytes); // 64 hex chars
    }

    /// <summary>Prefers X-Forwarded-For (set by a reverse proxy — see ForwardedHeaders
    /// middleware in Program.cs) over the raw socket address, so views are deduplicated by
    /// the real visitor IP even when the app is running behind nginx/Traefik/a load balancer.</summary>
    private static string ResolveClientIp(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwarded))
            return forwarded.Split(',')[0].Trim();

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
