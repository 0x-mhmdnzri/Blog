using BlogApp.Models;

namespace BlogApp.Models.ViewModels;

public class AdminDashboardViewModel
{
    public int TotalPosts { get; set; }
    public int PublishedPosts { get; set; }
    public int DraftPosts { get; set; }
    public int PendingComments { get; set; }
    public int ApprovedComments { get; set; }
    public int RejectedComments { get; set; }
    public int TotalMedia { get; set; }
    public long TotalMediaBytes { get; set; }
    public int TotalViews { get; set; }
    public int ViewsToday { get; set; }
    public int ViewsThisRange { get; set; }
    public int ViewsPreviousRange { get; set; }
    public double ViewsTrendPercent { get; set; }
    public int RangeDays { get; set; }

    public List<AdminCommentListItem> RecentComments { get; set; } = new();
    public List<ChartPoint> ViewsByDay { get; set; } = new();
    public List<ChartPoint> PostsByMonth { get; set; } = new();
    public List<NamedCount> PostsByCategory { get; set; } = new();
    public List<TopPostItem> TopPosts { get; set; } = new();

    public string DisplayName { get; set; } = string.Empty;
    public bool IsSuperAdmin { get; set; }
    public string ScopeLabel { get; set; } = string.Empty;
}

public class ChartPoint
{
    public string Label { get; set; } = string.Empty;
    public int Value { get; set; }
}

public class NamedCount
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class TopPostItem
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int Views { get; set; }
    public int RangeViews { get; set; }
}

public class AdminCommentListItem
{
    public int Id { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public CommentStatus Status { get; set; }
    public int PostId { get; set; }
    public string PostTitle { get; set; } = string.Empty;
    public string PostSlug { get; set; } = string.Empty;
}

public class AdminPostListItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public bool IsPublished { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsSticky { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? ScheduledPublishAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public int ViewCount { get; set; }
    public int CommentCount { get; set; }
    public int ReadingTimeMinutes { get; set; }
    public string AuthorDisplayName { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
}

public class ComingSoonViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
