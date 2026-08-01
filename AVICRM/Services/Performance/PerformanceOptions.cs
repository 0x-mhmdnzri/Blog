namespace AVICRM.Services.Performance;

public class PerformanceOptions
{
    public const string Section = "Performance";

    public CacheOptions Cache { get; set; } = new();
    public CdnOptions Cdn { get; set; } = new();
    public ImageOptimizeOptions ImageOptimize { get; set; } = new();
    public BackgroundJobsOptions Jobs { get; set; } = new();
}

public class CacheOptions
{
    /// <summary>memory | redis</summary>
    public string Provider { get; set; } = "memory";
    public string? RedisConnection { get; set; }
    public string RedisInstanceName { get; set; } = "AVICRM:";
    public int DefaultSeconds { get; set; } = 60;
    public int HomeFeedSeconds { get; set; } = 30;
    public int PostPageSeconds { get; set; } = 120;
    public int StaticMediaSeconds { get; set; } = 604800;
    public bool OutputCacheEnabled { get; set; } = true;
    public bool ResponseCacheEnabled { get; set; } = true;
}

public class CdnOptions
{
    public bool Enabled { get; set; }
    /// <summary>e.g. https://cdn.example.com — media URLs rewritten when set.</summary>
    public string BaseUrl { get; set; } = "";
    public bool RewriteMediaPaths { get; set; } = true;
}

public class ImageOptimizeOptions
{
    public bool Enabled { get; set; } = true;
    public int MaxWidth { get; set; } = 1920;
    public int JpegQuality { get; set; } = 82;
    public bool PreferWebP { get; set; } = true;
    /// <summary>Responsive srcset widths generated when source is larger.</summary>
    public int[] VariantWidths { get; set; } = [480, 800, 1280];
}

public class BackgroundJobsOptions
{
    public bool Enabled { get; set; } = true;
    public int PollIntervalMs { get; set; } = 2000;
    public int BatchSize { get; set; } = 8;
    public int Workers { get; set; } = 2;
}
