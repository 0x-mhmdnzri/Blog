using BlogApp.Services.Seo;

namespace BlogApp.Models.ViewModels;

public sealed class CrawlFeaturedItem
{
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string LanguageCode { get; set; } = "fa";
    public DateTime? PublishedAtUtc { get; set; }
}

public sealed class CrawlPhaseItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Metric { get; set; } = "";
    public string Detail { get; set; } = "";
    /// <summary>ok | warn | fail</summary>
    public string Status { get; set; } = "ok";
}

public sealed class CrawlPhaseCard
{
    public string Phase { get; set; } = "";
    public string Title { get; set; } = "";
    public List<CrawlPhaseItem> Items { get; set; } = new();
}

public sealed class CrawlMonitorViewModel
{
    public int RangeDays { get; set; }
    public BotCrawlSummaryDto Summary { get; set; } = new();
    public CrawlWasteReport Waste { get; set; } = new();
    public CrawlHealthAuditDto Audit { get; set; } = new();
    public OrphanPageReport? Orphans { get; set; }
    public ClickDepthReport? Depth { get; set; }
    public int FeaturedCount { get; set; }
    public int StickyCount { get; set; }
    public int PublishedCount { get; set; }
    public int NewsWindowPosts { get; set; }
    public int UpdatedLast7d { get; set; }
    public int OpenBacklinkLeads { get; set; }
    public int AcquiredBacklinks { get; set; }
    public int AuthoritySnapshotCount { get; set; }
    public string? LastAuthorityPeriod { get; set; }
    public int? LastAuthorityDr { get; set; }
    public List<CrawlPhaseCard> Phases { get; set; } = new();
    public List<CrawlFeaturedItem> RecentFeatured { get; set; } = new();
}
