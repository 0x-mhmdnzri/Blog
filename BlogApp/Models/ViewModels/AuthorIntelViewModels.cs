namespace BlogApp.Models.ViewModels;

public class AuthorIntelIndexViewModel
{
    public int RangeDays { get; set; } = 30;
    public List<AuthorIntelCard> Authors { get; set; } = new();
    public int TotalAuthors { get; set; }
    public long PlatformViews { get; set; }
    public int PlatformPosts { get; set; }
}

public class AuthorIntelCard
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool HasProfileImage { get; set; }
    public bool IsSuperAdmin { get; set; }
    public DateTime JoinedAtUtc { get; set; }
    public int PublishedPosts { get; set; }
    public int DraftPosts { get; set; }
    public long TotalViews { get; set; }
    public long ViewsInRange { get; set; }
    public int Followers { get; set; }
    public int CommentsReceived { get; set; }
    public int Reactions { get; set; }
    public int Bookmarks { get; set; }
    public int ReportsAgainst { get; set; }
    public double EngagementRate { get; set; }
    public double HealthScore { get; set; }
    public string HealthLabel { get; set; } = "—";
    public DateTime? LastPublishedAtUtc { get; set; }
    public double PostsPerWeek { get; set; }
}

public class AuthorIntelDetailViewModel
{
    public int RangeDays { get; set; } = 30;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public bool HasProfileImage { get; set; }
    public bool IsSuperAdmin { get; set; }
    public bool IsAuthor { get; set; }
    public DateTime JoinedAtUtc { get; set; }
    public string? Twitter { get; set; }
    public string? GitHub { get; set; }
    public string? Telegram { get; set; }
    public string? LinkedIn { get; set; }
    public string? Website { get; set; }

    public int PublishedPosts { get; set; }
    public int DraftPosts { get; set; }
    public int DeletedPosts { get; set; }
    public long TotalViews { get; set; }
    public long ViewsInRange { get; set; }
    public int Followers { get; set; }
    public int CommentsReceived { get; set; }
    public int Reactions { get; set; }
    public int Bookmarks { get; set; }
    public int Likes { get; set; }
    public int ReportsAgainst { get; set; }
    public int OpenReports { get; set; }
    public double EngagementRate { get; set; }
    public double AvgViewsPerPost { get; set; }
    public double AvgReadingMinutes { get; set; }
    public double AvgDwellSeconds { get; set; }
    public double PostsPerWeek { get; set; }
    public int PublishingStreakDays { get; set; }
    public double HealthScore { get; set; }
    public string HealthLabel { get; set; } = "—";
    public List<string> Insights { get; set; } = new();

    public List<AuthorIntelPoint> ViewsSeries { get; set; } = new();
    public List<AuthorIntelPoint> PublishSeries { get; set; } = new();
    public List<AuthorIntelBar> HourlyPublish { get; set; } = new();
    public List<AuthorIntelBar> WeekdayPublish { get; set; } = new();
    public List<AuthorIntelBar> Categories { get; set; } = new();
    public List<AuthorIntelBar> Tags { get; set; } = new();
    public List<AuthorIntelBar> Languages { get; set; } = new();
    public List<AuthorIntelBar> Devices { get; set; } = new();
    public List<AuthorIntelBar> Sources { get; set; } = new();
    public List<AuthorIntelTopPost> TopByViews { get; set; } = new();
    public List<AuthorIntelTopPost> TopByEngagement { get; set; } = new();
    public List<AuthorIntelTopPost> RecentPosts { get; set; } = new();
}

public class AuthorIntelPoint
{
    public string Label { get; set; } = string.Empty;
    public double Value { get; set; }
}

public class AuthorIntelBar
{
    public string Label { get; set; } = string.Empty;
    public double Value { get; set; }
    public double Pct { get; set; }
}

public class AuthorIntelTopPost
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int Views { get; set; }
    public int Comments { get; set; }
    public int Reactions { get; set; }
    public int Bookmarks { get; set; }
    public double EngagementRate { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public string? CategoryName { get; set; }
}
