using BlogApp.Data;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Services.Seo;

/// <summary>
/// Ongoing discipline — monthly crawl-health audit + crawl-budget series from BotCrawlHits.
/// Ground truth is server logs (not GSC alone). GSC fields are optional manual inputs.
/// </summary>
public sealed class CrawlDailyPoint
{
    public string Date { get; set; } = ""; // yyyy-MM-dd UTC
    public int Hits { get; set; }
    public int SearchHits { get; set; }
    public int AiHits { get; set; }
    public int WasteHits { get; set; }
    public int AvgMs { get; set; }
}

public sealed class CrawlHealthCheckItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
    public bool Pass { get; set; }
    public string Severity { get; set; } = "info"; // info | warn | fail
}

public sealed class CrawlHealthAuditDto
{
    public int Days { get; set; }
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public int Score { get; set; } // 0–100
    public string Grade { get; set; } = "—"; // A/B/C/D/F
    public List<CrawlHealthCheckItem> Checks { get; set; } = new();
    public List<CrawlDailyPoint> Daily { get; set; } = new();
    public int HitsPerDayAvg { get; set; }
    public int PeakDayHits { get; set; }
    public string? PeakDay { get; set; }
    public double SearchSharePct { get; set; }
    public double AiSharePct { get; set; }
}

public static class CrawlHealthAudit
{
    public static async Task<CrawlHealthAuditDto> BuildAsync(
        ApplicationDbContext db,
        BotCrawlSummaryDto summary,
        CrawlWasteReport? waste,
        int days = 30,
        CancellationToken ct = default)
    {
        days = Math.Clamp(days, 1, 90);
        var since = DateTime.UtcNow.AddDays(-days).Date;

        var hits = await db.BotCrawlHits.AsNoTracking()
            .Where(h => h.HitAtUtc >= since)
            .Select(h => new { h.HitAtUtc, h.BotKind, h.StatusCode, h.ElapsedMs })
            .ToListAsync(ct);

        var daily = hits
            .GroupBy(h => h.HitAtUtc.Date)
            .Select(g => new CrawlDailyPoint
            {
                Date = g.Key.ToString("yyyy-MM-dd"),
                Hits = g.Count(),
                SearchHits = g.Count(x => x.BotKind == "search"),
                AiHits = g.Count(x => x.BotKind == "ai"),
                WasteHits = g.Count(x => x.StatusCode is not (200 or 304)),
                AvgMs = g.Count() == 0 ? 0 : (int)g.Average(x => x.ElapsedMs)
            })
            .OrderBy(d => d.Date)
            .ToList();

        var filled = new List<CrawlDailyPoint>();
        for (var d = since; d <= DateTime.UtcNow.Date; d = d.AddDays(1))
        {
            var key = d.ToString("yyyy-MM-dd");
            var existing = daily.FirstOrDefault(x => x.Date == key);
            filled.Add(existing ?? new CrawlDailyPoint { Date = key });
        }

        var dto = new CrawlHealthAuditDto
        {
            Days = days,
            Daily = filled,
            HitsPerDayAvg = filled.Count == 0 ? 0 : (int)Math.Round(filled.Average(x => x.Hits)),
            PeakDayHits = filled.Count == 0 ? 0 : filled.Max(x => x.Hits),
            PeakDay = filled.Count == 0 ? null : filled.OrderByDescending(x => x.Hits).First().Date
        };

        if (summary.TotalHits > 0)
        {
            dto.SearchSharePct = Math.Round(100.0 * summary.SearchHits / summary.TotalHits, 1);
            dto.AiSharePct = Math.Round(100.0 * summary.AiHits / summary.TotalHits, 1);
        }

        var checks = new List<CrawlHealthCheckItem>();

        checks.Add(new CrawlHealthCheckItem
        {
            Id = "waste",
            Title = "Crawl waste (non-200/304) < 5%",
            Detail = summary.TotalHits == 0
                ? "No bot hits in window — baseline pending."
                : $"{summary.WastePercent:0.#}% waste · {summary.WasteHits:N0} / {summary.TotalHits:N0} hits",
            Pass = summary.TotalHits == 0 || summary.WastePercent < 5,
            Severity = summary.TotalHits == 0 ? "info" : (summary.WastePercent < 5 ? "info" : "fail")
        });

        checks.Add(new CrawlHealthCheckItem
        {
            Id = "ttfb_p50",
            Title = "TTFB median (p50) < 200ms",
            Detail = summary.TotalHits == 0
                ? "No data yet."
                : $"p50={summary.P50Ms}ms · p95={summary.P95Ms}ms · >200ms={summary.SlowOver200Pct:0.#}%",
            Pass = summary.TotalHits == 0 || summary.P50Ms < BotCrawlSummary.TtfbTargetMs,
            Severity = summary.TotalHits == 0 ? "info" : (summary.P50Ms < 200 ? "info" : "fail")
        });

        checks.Add(new CrawlHealthCheckItem
        {
            Id = "ttfb_slow_share",
            Title = "Share of bot hits > 200ms < 15%",
            Detail = summary.TotalHits == 0 ? "No data yet." : $"{summary.SlowOver200Pct:0.#}% of hits exceed TTFB target",
            Pass = summary.TotalHits == 0 || summary.SlowOver200Pct < 15,
            Severity = summary.TotalHits == 0 ? "info" : (summary.SlowOver200Pct < 15 ? "info" : "warn")
        });

        var chains = waste?.ChainCount ?? 0;
        checks.Add(new CrawlHealthCheckItem
        {
            Id = "redirect_chains",
            Title = "No multi-hop redirect chains",
            Detail = waste is null ? "Waste analyzer not loaded." : $"{chains} chain(s) · {waste.ActiveRedirects} active rules",
            Pass = waste is null || chains == 0,
            Severity = waste is null ? "info" : (chains == 0 ? "info" : "warn")
        });

        var qHits = waste?.BotHitsWithQuery ?? 0;
        checks.Add(new CrawlHealthCheckItem
        {
            Id = "query_hits",
            Title = "Bot hits with tracking/query params",
            Detail = waste is null ? "—" : $"{qHits:N0} hits with '?' in path (utm/gclid should 301-strip)",
            Pass = waste is null || qHits < Math.Max(10, summary.TotalHits / 20),
            Severity = waste is null ? "info" : (qHits == 0 ? "info" : "warn")
        });

        var notFound = waste?.BotNotFoundHits ?? 0;
        checks.Add(new CrawlHealthCheckItem
        {
            Id = "bot_404",
            Title = "Bot 404 rate reasonable",
            Detail = waste is null
                ? "—"
                : $"{notFound:N0} bot 404s · top paths listed below waste panel",
            Pass = waste is null || summary.TotalHits == 0 || (100.0 * notFound / Math.Max(1, summary.TotalHits)) < 8,
            Severity = waste is null ? "info" : (notFound == 0 ? "info" : "warn")
        });

        checks.Add(new CrawlHealthCheckItem
        {
            Id = "search_activity",
            Title = "Search crawler activity present",
            Detail = summary.TotalHits == 0
                ? "Waiting for Googlebot/Bingbot — ensure robots allow + sitemap submitted."
                : $"Search={summary.SearchHits:N0} · AI={summary.AiHits:N0} · Archive={summary.ArchiveHits:N0}",
            Pass = summary.SearchHits > 0 || summary.TotalHits == 0,
            Severity = summary.TotalHits == 0 ? "info" : (summary.SearchHits > 0 ? "info" : "warn")
        });

        checks.Add(new CrawlHealthCheckItem
        {
            Id = "retention",
            Title = "Bot log retention window (90d pipeline)",
            Detail = $"Audit window {days}d · hits/day avg={dto.HitsPerDayAvg:N0} · peak={dto.PeakDayHits:N0} on {dto.PeakDay ?? "—"}",
            Pass = true,
            Severity = "info"
        });

        dto.Checks = checks;

        var score = 100;
        foreach (var c in checks)
        {
            if (c.Severity == "fail" && !c.Pass) score -= 20;
            else if (c.Severity == "warn" && !c.Pass) score -= 8;
        }
        dto.Score = Math.Clamp(score, 0, 100);
        dto.Grade = dto.Score >= 90 ? "A"
            : dto.Score >= 80 ? "B"
            : dto.Score >= 70 ? "C"
            : dto.Score >= 50 ? "D"
            : "F";

        return dto;
    }
}
