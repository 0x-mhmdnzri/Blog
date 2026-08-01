using System.ComponentModel.DataAnnotations;
using AVICRM.Models;

namespace AVICRM.Models.ViewModels;

public class PostEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "عنوان الزامی است"), MaxLength(200, ErrorMessage = "عنوان نباید بیشتر از ۲۰۰ نویسه باشد")]
    [Display(Name = "عنوان")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(400, ErrorMessage = "خلاصه نباید بیشتر از ۴۰۰ نویسه باشد")]
    [Display(Name = "خلاصه")]
    public string? Summary { get; set; }

    [Required(ErrorMessage = "محتوای نوشته الزامی است")]
    [Display(Name = "محتوا (مارک‌داون)")]
    public string ContentMarkdown { get; set; } = string.Empty;

    public int? CategoryId { get; set; }

    [Display(Name = "برچسب‌ها (جدا شده با کاما)")]
    public string? TagsCsv { get; set; }

    public bool IsPublished { get; set; }

    [Display(Name = "انتشار زمان‌بندی‌شده")]
    public DateTime? ScheduledPublishAtUtc { get; set; }

    [Display(Name = "انقضای محتوا")]
    public DateTime? ExpiresAtUtc { get; set; }

    [Display(Name = "نوشته ویژه")]
    public bool IsFeatured { get; set; }

    [Display(Name = "نوشته چسبان")]
    public bool IsSticky { get; set; }

    public bool IsPremium { get; set; }
    public bool IsSponsored { get; set; }

    [MaxLength(120)]
    public string? SponsoredLabel { get; set; }

    public int? CoverMediaAssetId { get; set; }

    public int ReadingTimeMinutes { get; set; }

    [Display(Name = "زبان")]
    [MaxLength(8)]
    public string LanguageCode { get; set; } = AppCultures.Default;

    public TranslationStatus TranslationStatus { get; set; } = TranslationStatus.Original;

    public int? TranslationGroupId { get; set; }

    public List<PostTranslationLink> SiblingTranslations { get; set; } = new();

    public List<CategoryOption> AvailableCategories { get; set; } = new();

    public List<PostRevisionItem> Revisions { get; set; } = new();
}

public class CategoryOption
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class PostRevisionItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? Note { get; set; }
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
    public bool IsFeatured { get; set; }
    public bool IsSticky { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsPremium { get; set; }
    public int ReadingTimeMinutes { get; set; }
    public string LanguageCode { get; set; } = AppCultures.Default;
    public List<string> Tags { get; set; } = new();
}
