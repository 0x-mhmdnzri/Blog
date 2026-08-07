namespace BlogApp.Models.ViewModels;

/// <summary>Index: list of author intelligence cards for SuperAdmin.</summary>
public class AuthorIntelIndexViewModel
{
    public int TotalAuthors { get; set; }
    public int ActiveAuthors { get; set; }
    public int RisingAuthors { get; set; }
    public int DormantAuthors { get; set; }
    public List<AuthorIntelCard> Authors { get; set; } = new();
    public string? Sort { get; set; }
    public string? Q { get; set; }
}

public class AuthorIntelCard
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public bool HasAvatar { get; set; }
    public DateTime JoinedAtUtc { get; set; }
    public DateTime? LastPublishedAtUtc { get; set; }
    public DateTime? LastActiveAtUtc { get; set; }

    public int TotalPosts { get; set; }
    public int PublishedPosts { get; set; }
    public int DraftPosts { get; set; }
    public int TotalViews { get; set; }
    public int Views30d { get; set; }
    public int Likes { get; set; }
    public int Comments { get; set; }
    public int Followers { get; set; }
    public int Following { get; set; }
    public int OpenReports { get; set; }

    public int HealthScore { get; set; }
    public string HealthLabel { get; set; } = "Neutral";
    public string Momentum { get; set; } = "stable";
    public int CurrentStreakDays { get; set; }
    public int LongestStreakDays { get; set; }
    public double EngagementRate { get; set; }
    public int Posts30d { get; set; }
}

/// <summary>Full detail intelligence profile for one author.</summary>
public class AuthorIntelDetailViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? Email { get; set; }
    public bool HasAvatar { get; set; }
    public DateTime JoinedAtUtc { get; set; }
    public DateTime? LastPublishedAtUtc { get; set; }
    public DateTime? LastActiveAtUtc { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? TwitterHandle { get; set; }
    public string? GithubUsername { get; set; }
    public string? TelegramUsername { get; set; }

    public int HealthScore { get; set; }
    public string HealthLabel { get; set; } = "Neutral";
    public string Momentum { get; set; } = "stable";
    public List<string> Insights { get; set; } = new();
    public List<HealthFactor> HealthFactors { get; set; } = new();

    public int TotalPosts { get; set; }
    public int PublishedPosts { get; set; }
    public int DraftPosts { get; set; }
    public int DeletedPosts { get; set; }
    public int FeaturedPosts { get; set; }
    public int PremiumPosts { get; set; }
    public int Posts7d { get; set; }
    public int Posts30d { get; set; }
    public int Posts90d { get; set; }
    public int CurrentStreakDays { get; set; }
    public int LongestStreakDays { get; set; }
    public double AvgDaysBetweenPosts { get; set; }
    public int AvgWordCount { get; set; }
    public List<ChartPoint> PostsByDay { get; set; } = new();
    public List<ChartPoint> PostsByWeekday { get; set; } = new();
    public List<ChartPoint> PostsByHour { get; set; } = new();
    public List<NamedCount> PostsByCategory { get; set; } = new();
    public List<NamedCount> PostsByLanguage { get; set; } = new();
    public List<NamedCount> TopTags { get; set; } = new();

    public int TotalViews { get; set; }
    public int Views7d { get; set; }
    public int Views30d { get; set; }
    public int Views90d { get; set; }
    public int UniqueVisitors30d { get; set; }
    public int Likes { get; set; }
    public int Bookmarks { get; set; }
    public int CommentsReceived { get; set; }
    public int CommentsApproved { get; set; }
    public int Followers { get; set; }
    public int Following { get; set; }
    public double EngagementRate { get; set; }
    public double AvgViewsPerPost { get; set; }
    public double AvgReadSeconds { get; set; }
    public List<ChartPoint> ViewsByDay { get; set; } = new();
    public List<ChartPoint> ViewsByWeekday { get; set; } = new();
    public List<ChartPoint> ViewsByHour { get; set; } = new();

    public double ReturningVisitorPct { get; set; }
    public double BounceRatePct { get; set; }

    public int OpenReports { get; set; }
    public int ResolvedReports { get; set; }
    public int PendingReviewPosts { get; set; }
    public int RejectedPosts { get; set; }

    public List<AuthorIntelPostRow> TopPostsByViews { get; set; } = new();
    public List<AuthorIntelPostRow> TopPostsByEngagement { get; set; } = new();
    public List<AuthorIntelPostRow> RecentPosts { get; set; } = new();

    public List<AuthorIntelDayCell> ActivityGrid { get; set; } = new();
    public int ActivityYear { get; set; }
}

public class HealthFactor
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Score { get; set; }
    public string Hint { get; set; } = string.Empty;
}

public class AuthorIntelPostRow
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public stringSlug { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = "en";
    public bool IsPublished { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public int Views { get; set; }
    public int Likes { get; set; }
    public int Comments { get; set; }
    public int Bookmarks { get; set; }
    public double EngagementScore { get; set; }
    public string? CategoryName { get; set; }
}

public class AuthorIntelDayCell
{
    public DateTime Date { get; set; }
    public int Count { get; set; }
    public int Level { get; set; }
}
