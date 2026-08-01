using System.Text.RegularExpressions;

namespace AVICRM.Services.Analytics;

public sealed record ClientProfile(
    string DeviceType,
    string Browser,
    string Os,
    string TrafficSource,
    string? ReferrerHost,
    string? CountryCode);

public static class ClientHints
{
    public static ClientProfile From(HttpContext ctx)
    {
        var ua = ctx.Request.Headers.UserAgent.ToString() ?? "";
        var referer = ctx.Request.Headers.Referer.ToString();
        var host = ctx.Request.Host.Host;

        string? refHost = null;
        if (Uri.TryCreate(referer, UriKind.Absolute, out var refUri))
            refHost = refUri.Host.ToLowerInvariant();

        var source = ClassifySource(ctx, refHost, host);
        var device = DetectDevice(ua);
        var browser = DetectBrowser(ua);
        var os = DetectOs(ua);
        var country = ctx.Request.Headers["CF-IPCountry"].ToString();
        if (string.IsNullOrWhiteSpace(country) || country.Equals("XX", StringComparison.OrdinalIgnoreCase))
            country = ctx.Request.Headers["X-Country-Code"].ToString();
        if (string.IsNullOrWhiteSpace(country))
            country = null;
        else
            country = country.Trim().ToUpperInvariant();

        return new ClientProfile(device, browser, os, source, refHost, country);
    }

    private static string ClassifySource(HttpContext ctx, string? refHost, string ownHost)
    {
        var utm = ctx.Request.Query["utm_source"].ToString();
        if (!string.IsNullOrWhiteSpace(utm))
            return "utm:" + utm.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(refHost) || refHost.Equals(ownHost, StringComparison.OrdinalIgnoreCase))
            return "direct";

        if (IsSearchEngine(refHost)) return "search";
        if (IsSocial(refHost)) return "social";
        return "referral";
    }

    private static bool IsSearchEngine(string host) =>
        host.Contains("google.") || host.Contains("bing.") || host.Contains("yahoo.")
        || host.Contains("duckduckgo.") || host.Contains("baidu.") || host.Contains("yandex.");

    private static bool IsSocial(string host) =>
        host.Contains("twitter.") || host.Contains("x.com") || host.Contains("facebook.")
        || host.Contains("t.co") || host.Contains("linkedin.") || host.Contains("instagram.")
        || host.Contains("telegram.") || host.Contains("t.me") || host.Contains("reddit.");

    private static string DetectDevice(string ua)
    {
        ua = ua.ToLowerInvariant();
        if (Regex.IsMatch(ua, "bot|crawl|spider|slurp")) return "bot";
        if (ua.Contains("ipad") || ua.Contains("tablet")) return "tablet";
        if (ua.Contains("mobi") || ua.Contains("iphone") || ua.Contains("android"))
            return "mobile";
        return "desktop";
    }

    private static string DetectBrowser(string ua)
    {
        ua = ua.ToLowerInvariant();
        if (ua.Contains("edg/")) return "Edge";
        if (ua.Contains("chrome/") && !ua.Contains("edg/")) return "Chrome";
        if (ua.Contains("firefox/")) return "Firefox";
        if (ua.Contains("safari/") && !ua.Contains("chrome")) return "Safari";
        if (ua.Contains("opr/") || ua.Contains("opera")) return "Opera";
        return "Other";
    }

    private static string DetectOs(string ua)
    {
        ua = ua.ToLowerInvariant();
        if (ua.Contains("windows")) return "Windows";
        if (ua.Contains("android")) return "Android";
        if (ua.Contains("iphone") || ua.Contains("ipad") || ua.Contains("ios")) return "iOS";
        if (ua.Contains("mac os") || ua.Contains("macintosh")) return "macOS";
        if (ua.Contains("linux")) return "Linux";
        return "Other";
    }
}
