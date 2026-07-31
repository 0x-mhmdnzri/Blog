namespace BlogApp.Services.AdminSearch;

public sealed class AdminSearchRequest
{
    public string Q { get; set; } = string.Empty;
    public string Scope { get; set; } = "all";
    public int Take { get; set; } = 24;
    public int Skip { get; set; }
}

public sealed class AdminSearchHit
{
    public string EntityType { get; set; } = string.Empty;
    public string EntityKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? Snippet { get; set; }
    public string? Url { get; set; }
    public string? Icon { get; set; }
    public string? Status { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public double Score { get; set; }
    public string? RelativeTime { get; set; }
}

public sealed class AdminSearchResponse
{
    public string Query { get; set; } = string.Empty;
    public string Scope { get; set; } = "all";
    public long TotalHits { get; set; }
    public string TotalHitsLabel { get; set; } = string.Empty;
    public int TookMs { get; set; }
    public bool FromCache { get; set; }
    public IReadOnlyList<AdminSearchHit> Hits { get; set; } = Array.Empty<AdminSearchHit>();
    public IReadOnlyDictionary<string, int> CountsByType { get; set; } = new Dictionary<string, int>();
    public IReadOnlyList<string> Suggestions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Recent { get; set; } = Array.Empty<string>();
}
