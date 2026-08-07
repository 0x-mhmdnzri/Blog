using BlogApp.Data;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Services.Seo;

public sealed class BotCrawlFamilyStat
{
    public string Family { get; set; } = "";
    public string Kind { get; set; } = "";
    public int Hits { get; set; }
    public int AvgMs { get; set; }
    public int NonOk { get; set; }
}

public sealed class BotCrawlPathStat
{
    public string Path { get; set; } = "";
    public int Hits { get; set; }
    public int AvgMs { get; set; }
    public int LastStatus { get; set; }
}

public sealed class BotCrawlSummaryDto
{
    public int Days { get; set; }
    public int TotalHits { get; set; }
    public int SearchHits { get; set; }
    public int AiHits { get; set; }
    public int ArchiveHits { get; set; }
    public int WasteHits { get; set; }
    public double WastePercent { get; set; }
    public int AvgMs { get; set; }
    public int P95Ms { get; set; }
    public List<BotCrawlFamilyStat> ByFamily { get; set; } = new();
    public List<BotCrawlPathStat> TopPaths { get; set; } = new();
    public List<(int Code, int Count)> ByStatus { get; set; } = new();
}

public static class BotCrawlSummary
{
    public static async Task<BotCrawlSummaryDto> BuildAsync(ApplicationDbContext db, int days = 30, CancellationToken ct = default)
    {
        days = Math.Clamp(days, 1, 90);
        var since = DateTime.UtcNow.AddDays(-days);

        var rows = await db.BotCrawlHits.AsNoTracking()
            .Where(h => h.HitAtUtc >= since)
            .Select(h => new { h.BotFamily, h.BotKind, h.Path, h.StatusCode, h.ElapsedMs })
            .ToListAsync(ct);

        var dto = new BotCrawlSummaryDto { Days = days, TotalHits = rows.Count };
        if (rows.Count == 0) return dto;

        dto.SearchHits = rows.Count(r => r.BotKind == "search");
        dto.AiHits = rows.Count(r => r.BotKind == "ai");
        dto.ArchiveHits = rows.Count(r => r.BotKind == "archive");
        dto.WasteHits = rows.Count(r => r.StatusCode is not (200 or 304));
        dto.WastePercent = dto.TotalHits == 0 ? 0 : Math.Round(100.0 * dto.WasteHits / dto.TotalHits, 1);
        dto.AvgMs = (int)rows.Average(r => r.ElapsedMs);

        var sortedMs = rows.Select(r => r.ElapsedMs).OrderBy(x => x).ToList();
        dto.P95Ms = sortedMs[(int)Math.Clamp(Math.Ceiling(sortedMs.Count * 0.95) - 1, 0, sortedMs.Count - 1)];

        dto.ByFamily = rows
            .GroupBy(r => (r.BotFamily, r.BotKind))
            .Select(g => new BotCrawlFamilyStat
            {
                Family = g.Key.BotFamily,
                Kind = g.Key.BotKind,
                Hits = g.Count(),
                AvgMs = (int)g.Average(x => x.ElapsedMs),
                NonOk = g.Count(x => x.StatusCode is not (200 or 304))
            })
            .OrderByDescending(x => x.Hits)
            .Take(20)
            .ToList();

        dto.TopPaths = rows
            .GroupBy(r => r.Path)
            .Select(g => new BotCrawlPathStat
            {
                Path = g.Key,
                Hits = g.Count(),
                AvgMs = (int)g.Average(x => x.ElapsedMs),
                LastStatus = g.Last().StatusCode
            })
            .OrderByDescending(x => x.Hits)
            .Take(25)
            .ToList();

        dto.ByStatus = rows
            .GroupBy(r => r.StatusCode)
            .Select(g => (g.Key, g.Count()))
            .OrderByDescending(x => x.Item2)
            .ToList();

        return dto;
    }
}
