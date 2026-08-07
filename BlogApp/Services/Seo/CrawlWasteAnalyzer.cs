using BlogApp.Data;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Services.Seo;

public sealed class RedirectChainItem
{
    public string FromPath { get; set; } = "";
    public string FinalUrl { get; set; } = "";
    public int Hops { get; set; }
    public string PathVia { get; set; } = "";
}

public sealed class CrawlWasteReport
{
    public int ActiveRedirects { get; set; }
    public int ChainCount { get; set; }
    public List<RedirectChainItem> Chains { get; set; } = new();
    public int BotHitsWithQuery { get; set; }
    public int BotRedirectHits { get; set; }
    public int BotNotFoundHits { get; set; }
    public List<(string Path, int Hits)> Top404Paths { get; set; } = new();
    public List<(string Path, int Hits)> TopRedirectPaths { get; set; } = new();
}

public static class CrawlWasteAnalyzer
{
    public static async Task<CrawlWasteReport> BuildAsync(ApplicationDbContext db, int days = 30, CancellationToken ct = default)
    {
        days = Math.Clamp(days, 1, 90);
        var report = new CrawlWasteReport();

        var rules = await db.RedirectRules.AsNoTracking()
            .Where(r => r.IsActive)
            .Select(r => new { r.FromPath, r.ToUrl })
            .ToListAsync(ct);

        report.ActiveRedirects = rules.Count;
        var byFrom = rules
            .GroupBy(r => r.FromPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().ToUrl, StringComparer.OrdinalIgnoreCase);

        foreach (var r in rules)
        {
            var hops = 1;
            var current = r.ToUrl;
            var via = new List<string> { r.FromPath };
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { r.FromPath };

            for (var i = 0; i < 5; i++)
            {
                var nextPath = ToPath(current);
                if (nextPath is null || !seen.Add(nextPath)) break;
                if (!byFrom.TryGetValue(nextPath, out var nextTo)) break;
                via.Add(nextPath);
                current = nextTo;
                hops++;
            }

            if (hops > 1)
            {
                report.ChainCount++;
                report.Chains.Add(new RedirectChainItem
                {
                    FromPath = r.FromPath,
                    FinalUrl = current,
                    Hops = hops,
                    PathVia = string.Join(" → ", via) + " → " + current
                });
            }
        }

        report.Chains = report.Chains.OrderByDescending(c => c.Hops).Take(30).ToList();

        var since = DateTime.UtcNow.AddDays(-days);
        var botRows = await db.BotCrawlHits.AsNoTracking()
            .Where(h => h.HitAtUtc >= since)
            .Select(h => new { h.Path, h.StatusCode })
            .ToListAsync(ct);

        report.BotHitsWithQuery = botRows.Count(r => r.Path.Contains('?', StringComparison.Ordinal));
        report.BotRedirectHits = botRows.Count(r => r.StatusCode is >= 300 and < 400);
        report.BotNotFoundHits = botRows.Count(r => r.StatusCode == 404);

        report.Top404Paths = botRows
            .Where(r => r.StatusCode == 404)
            .GroupBy(r => r.Path)
            .Select(g => (g.Key, g.Count()))
            .OrderByDescending(x => x.Item2)
            .Take(15)
            .ToList();

        report.TopRedirectPaths = botRows
            .Where(r => r.StatusCode is >= 300 and < 400)
            .GroupBy(r => r.Path)
            .Select(g => (g.Key, g.Count()))
            .OrderByDescending(x => x.Item2)
            .Take(15)
            .ToList();

        return report;
    }

    private static string? ToPath(string toUrl)
    {
        if (string.IsNullOrWhiteSpace(toUrl)) return null;
        toUrl = toUrl.Trim();
        if (toUrl.StartsWith('/') && !toUrl.StartsWith("//", StringComparison.Ordinal))
        {
            var q = toUrl.IndexOf('?', StringComparison.Ordinal);
            return q >= 0 ? toUrl[..q] : toUrl;
        }
        if (Uri.TryCreate(toUrl, UriKind.Absolute, out var uri))
            return string.IsNullOrEmpty(uri.AbsolutePath) ? "/" : uri.AbsolutePath;
        return null;
    }
}
