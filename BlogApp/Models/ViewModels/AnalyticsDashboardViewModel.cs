namespace BlogApp.Models.ViewModels;

/// <summary>
/// Deep visitor / behavior analytics (AdminAnalytics).
/// Distinct from AdminDashboardViewModel which is operational CMS health.
/// </summary>
public class AnalyticsDashboardViewModel
{
    public int RangeDays { get; set; }
    public int TotalViews { get; set; }
    public int UniqueVisitors { get; set; }
    public double BounceRatePercent { get; set; }
    public double AvgReadingSeconds { get; set; }

    /// <summary>Views per unique visitor in range.</summary>
    public double ViewsPerVisitor { get; set; }

    /// <summary>Share of range views that are returning visitors (seen before range).</summary>
    public double ReturningVisitorPercent { get; set; }

    public int SessionCount { get; set; }
    public int HeatmapClickCount { get; set; }
    public int SearchQueryCount { get; set; }

    public List<ChartPoint> ViewsByDay { get; set; } = new();
    public List<ChartPoint> ViewsByHour { get; set; } = new();
    public List<NamedCount> TrafficSources { get; set; } = new();
    public List<NamedCount> Devices { get; set; } = new();
    public List<NamedCount> Browsers { get; set; } = new();
    public List<NamedCount> OperatingSystems { get; set; } = new();
    public List<NamedCount> Countries { get; set; } = new();
    public List<NamedCount> Referrers { get; set; } = new();
    public List<NamedCount> SearchKeywords { get; set; } = new();
    public List<TopPostItem> PopularPosts { get; set; } = new();
    public List<TopPostItem> TrendingPosts { get; set; } = new();
    public List<HeatmapPoint> Heatmap { get; set; } = new();
    public int? HeatmapPostId { get; set; }
    public string? HeatmapPostTitle { get; set; }
    public List<(int Id, string Title)> HeatmapPostOptions { get; set; } = new();
}

public class HeatmapPoint
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Count { get; set; }
}
