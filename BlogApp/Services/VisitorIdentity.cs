using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace BlogApp.Services;

/// <summary>
/// One-way visitor fingerprint = SHA-256(normalized IP + "|" + normalized User-Agent).
/// Same person reloading the same post is treated as one view; different browsers or
/// different networks count separately. Raw IP/UA are never stored — only the hash.
/// </summary>
public static class VisitorIdentity
{
    /// <summary>How long the same IP+UA counts as a single view on a given post.</summary>
    public static readonly TimeSpan DedupeWindow = TimeSpan.FromHours(24);

    public static string ComputeHash(HttpContext context)
    {
        var ip = NormalizeIp(ResolveClientIp(context));
        var userAgent = NormalizeUserAgent(context.Request.Headers.UserAgent.ToString());

        // Mixture of IP address and User-Agent — both required for the fingerprint.
        var material = Encoding.UTF8.GetBytes(ip + "|" + userAgent);
        var hash = SHA256.HashData(material);
        return Convert.ToHexString(hash); // 64 hex chars, uppercase
    }

    /// <summary>
    /// Prefers the first hop in X-Forwarded-For (set by a reverse proxy) so the real
    /// client IP is used behind nginx/Traefik/Caddy/cloud LB.
    /// </summary>
    private static string ResolveClientIp(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var first = forwarded.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
            if (!string.IsNullOrWhiteSpace(first))
                return first;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    /// <summary>Map ::ffff:x.x.x.x → x.x.x.x so dual-stack localhost does not double-count.</summary>
    private static string NormalizeIp(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return "unknown";
        ip = ip.Trim().Trim('[', ']');

        if (IPAddress.TryParse(ip, out var addr))
        {
            if (addr.IsIPv4MappedToIPv6)
                addr = addr.MapToIPv4();
            return addr.ToString();
        }

        return ip.ToLowerInvariant();
    }

    private static string NormalizeUserAgent(string ua)
    {
        if (string.IsNullOrWhiteSpace(ua)) return "empty-ua";
        // Collapse whitespace; keep case-insensitive stability across minor header noise.
        return string.Join(' ', ua.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
    }
}
