using System.ComponentModel.DataAnnotations;

namespace BlogApp.Models.ViewModels;

public class PostEditViewModel
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    [Display(Name = "Title")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(400)]
    [Display(Name = "Summary")]
    public string? Summary { get; set; }

    [Required]
    [Display(Name = "Content (Markdown)")]
    public string ContentMarkdown { get; set; } = string.Empty;

    public int? CategoryId { get; set; }

    [Display(Name = "Tags (comma separated)")]
    public string? TagsCsv { get; set; }

    public bool IsPublished { get; set; }

    public int? CoverMediaAssetId { get; set; }

    public List<CategoryOption> AvailableCategories { get; set; } = new();
}

public class CategoryOption
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class PostListItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? CategoryName { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public int? CoverMediaAssetId { get; set; }
    public bool IsPublished { get; set; }
    public List<string> Tags { get; set; } = new();
}
