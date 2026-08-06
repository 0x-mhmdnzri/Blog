namespace BlogApp.Models.ViewModels;

public class TaxonomyAdminViewModel
{
    public List<FolderAdminItem> Folders { get; set; } = new();
    public List<CategoryTreeItem> Categories { get; set; } = new();
    public List<CategoryOption> ParentOptions { get; set; } = new();
    public List<TagAdminItem> Tags { get; set; } = new();
    public List<SeriesAdminItem> Series { get; set; } = new();
    public List<TopicAdminItem> Topics { get; set; } = new();
}

public class FolderAdminItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "blue";
    public int PostCount { get; set; }
    public int? ParentId { get; set; }
}

public class FolderDetailViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "blue";
    public string? Search { get; set; }
    public string? Sort { get; set; }
    public int? CategoryId { get; set; }
    public int? TagId { get; set; }
    public List<FolderPostItem> Posts { get; set; } = new();
    public List<CategoryOption> Categories { get; set; } = new();
    public List<TagAdminItem> Tags { get; set; } = new();
}

public class FolderPostItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = "fa";
    public bool IsPublished { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string? CategoryName { get; set; }
    public int? CategoryId { get; set; }
    public List<string> TagNames { get; set; } = new();
    public int SortOrder { get; set; }
}

public class CategoryTreeItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ParentId { get; set; }
    public int Depth { get; set; }
    public int PostCount { get; set; }
}

// CategoryOption is defined in PostEditViewModel.cs (shared)

public class TagAdminItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int PostCount { get; set; }
}

public class SeriesAdminItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int PostCount { get; set; }
}

public class TopicAdminItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublished { get; set; }
    public int ItemCount { get; set; }
}

public class RelatedPostItem
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public int SharedTagCount { get; set; }
}
