namespace BlogApp.Models.ViewModels;

public class AnalyticsDashboardViewModel
{
    public int RangeDays { get; set; }
    public int TotalViews { get; set; }
    public int UniqueVisitors { get; set; }
    public double BounceRatePercent { get; set; }
    public double AvgReadingSeconds { get; set; }

    public List<ChartPoint> ViewsByDay { get; set; } = new();
    public List<NamedCount> TrafficSources { get; set; } = new();
    public List<NamedCount> Devices { get; set; } = new();
    public List<NamedCount> Browsers { get; set; } = new();
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
