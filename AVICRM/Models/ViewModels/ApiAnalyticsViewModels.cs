namespace AVICRM.Models.ViewModels;

public class ApiUserUsageRow
{
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public int KeyCount { get; set; }
    public int ActiveKeys { get; set; }
    public int BannedKeys { get; set; }
    public long LifetimeRequests { get; set; }
    public int RangeRequests { get; set; }
    public int RangeErrors { get; set; }
    public int RangeRateLimited { get; set; }
    public double AvgDurationMs { get; set; }
    public DateTime? LastCallUtc { get; set; }
}

public class ApiEndpointUsageRow
{
    public string Method { get; set; } = "";
    public string Path { get; set; } = "";
    public int Count { get; set; }
    public int Errors { get; set; }
    public double AvgMs { get; set; }
}

public class ApiAnalyticsPanel
{
    public int TotalRequests { get; set; }
    public int ErrorCount { get; set; }
    public int RateLimitedCount { get; set; }
    public int UniqueUsers { get; set; }
    public int ActiveKeys { get; set; }
    public int BannedKeys { get; set; }
    public double AvgDurationMs { get; set; }
    public List<ChartPoint> RequestsByDay { get; set; } = new();
    public List<ApiUserUsageRow> Users { get; set; } = new();
    public List<ApiEndpointUsageRow> TopEndpoints { get; set; } = new();
    public List<NamedCount> StatusCodes { get; set; } = new();
}
