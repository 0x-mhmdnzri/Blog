namespace BlogApp.Controllers;

/// <summary>Lightweight folder chip for the public blog feed.</summary>
public sealed class BlogFeedFolderItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "blue";
    public int Count { get; set; }
}

/// <summary>Series hub chip for home discovery (P2.1).</summary>
public sealed class BlogFeedSeriesItem
{
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public int Count { get; set; }
}
