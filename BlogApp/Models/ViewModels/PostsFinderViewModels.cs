using BlogApp.Models;

namespace BlogApp.Models.ViewModels;

public class PostsFinderViewModel
{
    public string Scope { get; set; } = "all";
    public int? FolderId { get; set; }
    public int? CategoryId { get; set; }
    public int? TagId { get; set; }
    public string? Search { get; set; }
    public string Sort { get; set; } = "recent";
    public bool ShowAuthor { get; set; }

    /// <summary>User is Author or SuperAdmin — may see non-public scopes and owner actions.</summary>
    public bool CanManage { get; set; }

    /// <summary>SuperAdmin (or claim) can act on any post; authors only own.</summary>
    public bool CanManageAll { get; set; }

    public string? CurrentUserId { get; set; }

    /// <summary>Public /Blog browser (vs staff /Admin/Posts).</summary>
    public bool IsPublicSurface { get; set; }

    public int AllCount { get; set; }
    public int PublishedCount { get; set; }
    public int DraftCount { get; set; }
    public int PendingCount { get; set; }
    public int TrashCount { get; set; }

    public List<FinderPostItem> Posts { get; set; } = new();
    public List<FinderFolderItem> Folders { get; set; } = new();
    public List<FinderSideItem> Categories { get; set; } = new();
    public List<FinderSideItem> Tags { get; set; } = new();

    public bool CanActOn(FinderPostItem p) =>
        CanManage && !p.IsDeleted && (CanManageAll || (!string.IsNullOrEmpty(CurrentUserId) && p.AuthorId == CurrentUserId));
}

public class FinderPostItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public int? CategoryId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsFeatured { get; set; }
    public PostReviewStatus ReviewStatus { get; set; }
    public DateTime? ScheduledPublishAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public int ViewCount { get; set; }
    public string? CoverUrl { get; set; }
    public List<string> TagNames { get; set; } = new();
}

public class FinderFolderItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "blue";
    public int PostCount { get; set; }
}

public class FinderSideItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}
