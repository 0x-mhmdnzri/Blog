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
    public int TotalViews { get; set; }
    public List<AdminCommentListItem> RecentComments { get; set; } = new();
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
    public DateTime CreatedAtUtc { get; set; }
    public int ViewCount { get; set; }
    public int CommentCount { get; set; }
}

public class ComingSoonViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
