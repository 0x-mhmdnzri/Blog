using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using BlogApp.Services.Seo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public partial class AdminAnalyticsController
{
    /// <summary>
    /// SuperAdmin crawl / indexing monitor — one panel per PRD phase (P0–P4 + ongoing).
    /// Ground truth: BotCrawlHits + structural analyzers (orphans, depth, authority).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Crawl(int range = 30)
    {
        if (range is not (7 or 30 or 90)) range = 30;

        var summary = await BotCrawlSummary.BuildAsync(_db, range);
        var waste = await CrawlWasteAnalyzer.BuildAsync(_db, range);
        var audit = await CrawlHealthAudit.BuildAsync(_db, summary, waste, range);

        OrphanPageReport? orphans = null;
        try
        {
            orphans = await OrphanPageAnalyzer.BuildAsync(_db, configuredBaseUrl: null);
        }
        catch { /* analyzer optional if schema lag */ }

        ClickDepthReport? depth = null;
        try
        {
            depth = await ClickDepthAnalyzer.BuildAsync(_db);
        }
        catch { }

        var now = DateTime.UtcNow;
        var featuredCount = await _db.Posts.AsNoTracking()
            .CountAsync(p => p.IsPublished && !p.IsDeleted && p.IsFeatured);
        var stickyCount = await _db.Posts.AsNoTracking()
            .CountAsync(p => p.IsPublished && !p.IsDeleted && p.IsSticky);
        var publishedCount = await _db.Posts.AsNoTracking()
            .CountAsync(p => p.IsPublished && !p.IsDeleted);
        var recentFeatured = await _db.Posts.AsNoTracking()
            .Where(p => p.IsPublished && !p.IsDeleted && p.IsFeatured
                        && p.PublishedAtUtc != null
                        && p.PublishedAtUtc >= now.AddDays(-14))
            .OrderByDescending(p => p.PublishedAtUtc)
            .Take(8)
            .Select(p => new { p.Title, p.Slug, p.LanguageCode, p.PublishedAtUtc })
            .ToListAsync();

        var leadOpen = 0;
        var leadAcquired = 0;
        var snapCount = 0;
        AuthoritySnapshot? lastSnap = null;
        try
        {
            leadOpen = await _db.BacklinkLeads.AsNoTracking()
                .CountAsync(l => l.Status == "prospect" || l.Status == "contacted" || l.Status == "negotiated");
            leadAcquired = await _db.BacklinkLeads.AsNoTracking()
                .CountAsync(l => l.Status == "acquired");
            snapCount = await _db.AuthoritySnapshots.AsNoTracking().CountAsync();
            lastSnap = await _db.AuthoritySnapshots.AsNoTracking()
                .OrderByDescending(s => s.MeasuredAtUtc)
                .FirstOrDefaultAsync();
        }
        catch { /* tables may not exist on older DBs until bootstrap */ }

        // Sitemap / freshness signals
        var newsWindow = await _db.Posts.AsNoTracking()
            .CountAsync(p => p.IsPublished && !p.IsDeleted
                             && p.PublishedAtUtc != null
                             && p.PublishedAtUtc >= now.AddHours(-48));
        var updated7d = await _db.Posts.AsNoTracking()
            .CountAsync(p => p.IsPublished && !p.IsDeleted
                             && p.UpdatedAtUtc >= now.AddDays(-7));

        var phases = BuildPhaseCards(summary, waste, orphans, depth, featuredCount, stickyCount,
            leadOpen, leadAcquired, snapCount, lastSnap, newsWindow, updated7d, publishedCount, audit);

        var vm = new CrawlMonitorViewModel
        {
            RangeDays = range,
            Summary = summary,
            Waste = waste,
            Audit = audit,
            Orphans = orphans,
            Depth = depth,
            FeaturedCount = featuredCount,
            StickyCount = stickyCount,
            PublishedCount = publishedCount,
            NewsWindowPosts = newsWindow,
            UpdatedLast7d = updated7d,
            OpenBacklinkLeads = leadOpen,
            AcquiredBacklinks = leadAcquired,
            AuthoritySnapshotCount = snapCount,
            LastAuthorityPeriod = lastSnap?.Period,
            LastAuthorityDr = lastSnap?.DomainRating ?? lastSnap?.DomainAuthority,
            Phases = phases,
            RecentFeatured = recentFeatured.Select(p => new CrawlFeaturedItem
            {
                Title = p.Title,
                Slug = p.Slug,
                LanguageCode = p.LanguageCode,
                PublishedAtUtc = p.PublishedAtUtc
            }).ToList()
        };

        ViewData["Title"] = "Crawl & Indexing Monitor";
        return View(vm);
    }

    private static List<CrawlPhaseCard> BuildPhaseCards(
        BotCrawlSummaryDto s,
        CrawlWasteReport w,
        OrphanPageReport? o,
        ClickDepthReport? d,
        int featured, int sticky,
        int leadOpen, int leadAcquired, int snaps,
        AuthoritySnapshot? lastSnap,
        int newsWindow, int updated7d, int published,
        CrawlHealthAuditDto audit)
    {
        string StatusFrom(bool ok, bool warn = false) =>
            ok ? "ok" : (warn ? "warn" : "fail");

        return new List<CrawlPhaseCard>
        {
            new()
            {
                Phase = "P0",
                Title = "Foundational",
                Items = new List<CrawlPhaseItem>
                {
                    new()
                    {
                        Id = "P0.1",
                        Title = "Bot log pipeline",
                        Metric = s.TotalHits == 0 ? "No hits yet" : $"{s.TotalHits:N0} hits · search {s.SearchHits:N0} / AI {s.AiHits:N0}",
                        Status = StatusFrom(s.TotalHits > 0, warn: s.TotalHits == 0),
                        Detail = "BotCrawlHits 90d retention"
                    },
                    new()
                    {
                        Id = "P0.2",
                        Title = "TTFB under crawl load",
                        Metric = s.TotalHits == 0 ? "—" : $"p50 {s.P50Ms}ms · p95 {s.P95Ms}ms · >200ms {s.SlowOver200Pct:0.#}%",
                        Status = StatusFrom(s.TotalHits == 0 || s.P50Ms < 200, warn: s.P50Ms is >= 200 and < 400),
                        Detail = "Target p50 < 200ms"
                    },
                    new()
                    {
                        Id = "P0.3",
                        Title = "robots.txt AI policy",
                        Metric = "Policy shipped",
                        Status = "ok",
                        Detail = "Allow public · disallow admin/api"
                    }
                }
            },
            new()
            {
                Phase = "P1",
                Title = "Crawl waste elimination",
                Items = new List<CrawlPhaseItem>
                {
                    new()
                    {
                        Id = "P1.1",
                        Title = "Redirect chains / soft 404s / query waste",
                        Metric = $"chains {w.ChainCount} · bot ?query {w.BotHitsWithQuery:N0} · 404 {w.BotNotFoundHits:N0}",
                        Status = StatusFrom(w.ChainCount == 0 && (s.TotalHits == 0 || s.WastePercent < 5),
                            warn: w.ChainCount > 0 || s.WastePercent >= 5),
                        Detail = $"Waste {s.WastePercent:0.#}% (target <5%)"
                    },
                    new()
                    {
                        Id = "P1.2",
                        Title = "Orphan pages",
                        Metric = o is null ? "Analyzer unavailable" : $"{o.OrphanCount} orphans / {o.PublishedCount} published",
                        Status = o is null ? "warn" : StatusFrom(o.OrphanCount == 0, warn: o.OrphanCount > 0 && o.OrphanCount < 10),
                        Detail = o is null ? "—" : $"{o.WithContentInlinks} with content inlinks"
                    },
                    new()
                    {
                        Id = "P1.3",
                        Title = "Clean split sitemaps",
                        Metric = "sitemap index + children live",
                        Status = "ok",
                        Detail = "pages / posts / authors / taxonomies / news"
                    }
                }
            },
            new()
            {
                Phase = "P2",
                Title = "Discoverability / hubs",
                Items = new List<CrawlPhaseItem>
                {
                    new()
                    {
                        Id = "P2.1",
                        Title = "Click depth ≤4",
                        Metric = d is null
                            ? "Analyzer unavailable"
                            : $"beyond-4: {d.Beyond4Count} · unreachable: {d.UnreachableCount}",
                        Status = d is null ? "warn" : StatusFrom(d.Beyond4Count == 0 && d.UnreachableCount == 0,
                            warn: d.Beyond4Count > 0),
                        Detail = d is null ? "—" : $"max depth observed {d.MaxDepth}"
                    },
                    new()
                    {
                        Id = "P2.2",
                        Title = "Hub links on publish",
                        Metric = $"featured {featured} · sticky {sticky} · 14d window active",
                        Status = StatusFrom(featured > 0 || published == 0, warn: published > 0 && featured == 0),
                        Detail = "Auto-IsFeatured + footer Latest"
                    }
                }
            },
            new()
            {
                Phase = "P3",
                Title = "Freshness & demand signals",
                Items = new List<CrawlPhaseItem>
                {
                    new()
                    {
                        Id = "P3.1",
                        Title = "Update cadence + News sitemap",
                        Metric = $"news window (48h) {newsWindow} · updated 7d {updated7d}",
                        Status = StatusFrom(true),
                        Detail = "Dynamic changefreq / priority + IndexNow"
                    },
                    new()
                    {
                        Id = "P3.2",
                        Title = "Structured data + mobile parity",
                        Metric = "JSON-LD + OG cards + viewport",
                        Status = "ok",
                        Detail = "GitHub-style /og cards · max-image-preview:large"
                    }
                }
            },
            new()
            {
                Phase = "P4",
                Title = "Authority (ops)",
                Items = new List<CrawlPhaseItem>
                {
                    new()
                    {
                        Id = "P4.1",
                        Title = "Backlink leads",
                        Metric = $"open {leadOpen} · acquired {leadAcquired}",
                        Status = StatusFrom(leadAcquired > 0 || leadOpen > 0, warn: leadOpen == 0 && leadAcquired == 0),
                        Detail = "SEO Authority tracker"
                    },
                    new()
                    {
                        Id = "P4.2",
                        Title = "Quarterly DA/DR",
                        Metric = snaps == 0
                            ? "No snapshots yet"
                            : $"{snaps} snapshots · last {lastSnap?.Period ?? "—"} DR/DA {lastSnap?.DomainRating ?? lastSnap?.DomainAuthority ?? 0}",
                        Status = StatusFrom(snaps > 0, warn: snaps == 0),
                        Detail = "One entry per quarter — not weekly noise"
                    }
                }
            },
            new()
            {
                Phase = "Ongoing",
                Title = "Discipline",
                Items = new List<CrawlPhaseItem>
                {
                    new()
                    {
                        Id = "Audit",
                        Title = "Monthly crawl-health audit",
                        Metric = $"grade {audit.Grade} · score {audit.Score}/100",
                        Status = audit.Score >= 80 ? "ok" : (audit.Score >= 60 ? "warn" : "fail"),
                        Detail = $"avg {audit.HitsPerDayAvg:N0}/day · peak {audit.PeakDayHits:N0} on {audit.PeakDay ?? "—"}"
                    },
                    new()
                    {
                        Id = "Budget",
                        Title = "Crawl budget dashboard",
                        Metric = $"search {audit.SearchSharePct:0.#}% · AI {audit.AiSharePct:0.#}%",
                        Status = StatusFrom(audit.Daily.Count > 0, warn: audit.Daily.Count == 0),
                        Detail = $"{audit.Days}d daily series on this page"
                    }
                }
            }
        };
    }
}
