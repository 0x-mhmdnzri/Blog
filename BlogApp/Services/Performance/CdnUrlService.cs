using Microsoft.Extensions.Options;

namespace BlogApp.Services.Performance;

public sealed class CdnUrlService : ICdnUrlService
{
    private readonly CdnOptions _cdn;

    public CdnUrlService(IOptions<PerformanceOptions> opt)
    {
        _cdn = opt.Value.Cdn;
    }

    public bool IsEnabled =>
        _cdn.Enabled && !string.IsNullOrWhiteSpace(_cdn.BaseUrl);

    public string MediaUrl(int mediaId) =>
        Resolve($"/media/{mediaId}");

    public string Resolve(string? relativeOrAbsolute)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolute))
            return string.Empty;

        if (relativeOrAbsolute.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || relativeOrAbsolute.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return relativeOrAbsolute;

        if (!IsEnabled || !_cdn.RewriteMediaPaths)
            return relativeOrAbsolute.StartsWith('/') ? relativeOrAbsolute : "/" + relativeOrAbsolute;

        var baseUrl = _cdn.BaseUrl.TrimEnd('/');
        var path = relativeOrAbsolute.StartsWith('/') ? relativeOrAbsolute : "/" + relativeOrAbsolute;
        return baseUrl + path;
    }
}
