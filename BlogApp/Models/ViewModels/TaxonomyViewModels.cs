namespace BlogApp.Models.ViewModels;

public class TaxonomyAdminViewModel
{
    public List<CategoryTreeItem> Categories { get; set; } = new();
    public List<CategoryOption> ParentOptions { get; set; } = new();
    public List<TagAdminItem> Tags { get; set; } = new();
    public List<SeriesAdminItem> Series { get; set; } = new();
    public List<TopicAdminItem> Topics { get; set; } = new();
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
