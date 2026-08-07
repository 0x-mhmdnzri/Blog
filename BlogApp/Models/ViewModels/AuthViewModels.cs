using System.ComponentModel.DataAnnotations;
using BlogApp.Attributes;
using BlogApp.Models;
using Microsoft.AspNetCore.Http;

namespace BlogApp.Models.ViewModels;

public class ProfileEditViewModel
{
    [Required(ErrorMessage = "نام نمایشی الزامی است"), MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Bio { get; set; }

    [EmailAddress]
    public string? Email { get; set; }

    public UserGender Gender { get; set; } = UserGender.Unspecified;

    [MaxLength(120)]
    public string? Twitter { get; set; }

    [MaxLength(200)]
    public string? LinkedIn { get; set; }

    [MaxLength(120)]
    public string? Telegram { get; set; }

    [MaxLength(40)]
    [Phone]
    public string? Phone { get; set; }

    [MaxLength(200)]
    [Url]
    public string? Website { get; set; }

    [MaxLength(120)]
    public string? GitHub { get; set; }

    [MaxLength(120)]
    public string? Instagram { get; set; }

    public IFormFile? ProfileImageFile { get; set; }
    public bool RemoveProfileImage { get; set; }
    public bool HasProfileImage { get; set; }
}
public class CreateAuthorViewModel
{
    [Required(ErrorMessage = "نام کاربری الزامی است")]
    [MaxLength(50, ErrorMessage = "حداکثر ۵۰ نویسه")]
    [RegularExpression(@"^[a-zA-Z0-9._-]{3,50}$",
        ErrorMessage = "۳ تا ۵۰ نویسه: حروف، اعداد، . _ -")]
    [Display(Name = "نام کاربری")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "ایمیل الزامی است")]
    [EmailAddress(ErrorMessage = "ایمیل نامعتبر است")]
    [MaxLength(256, ErrorMessage = "حداکثر ۲۵۶ نویسه")]
    [Display(Name = "ایمیل")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "نام نمایشی الزامی است")]
    [MaxLength(100, ErrorMessage = "حداکثر ۱۰۰ نویسه")]
    [MinLength(2, ErrorMessage = "حداقل ۲ نویسه")]
    [Display(Name = "نام نمایشی")]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "حداکثر ۵۰۰ نویسه")]
    [Display(Name = "بیوگرافی")]
    public string? Bio { get; set; }

    [Display(Name = "جنسیت")]
    public UserGender Gender { get; set; } = UserGender.Unspecified;

    public string? Twitter { get; set; }

    public string? LinkedIn { get; set; }

    public string? Telegram { get; set; }

    public string? Phone { get; set; }

    public string? Website { get; set; }
    public string? GitHub { get; set; }

    public string? Instagram { get; set; }

    public IFormFile? ProfileImageFile { get; set; }

    [Required(ErrorMessage = "رمز عبور الزامی است")]
    [MinLength(10, ErrorMessage = "حداقل ۱۰ نویسه")]
    [MaxLength(128, ErrorMessage = "حداکثر ۱۲۸ نویسه")]
    [DataType(DataType.Password)]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).{10,}$",
        ErrorMessage = "حداقل ۱۰ نویسه، شامل حرف بزرگ، کوچک، عدد و نماد (مثل !@#)")]
    [Display(Name = "رمز عبور")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "تکرار رمز عبور الزامی است")]
    [Compare(nameof(Password), ErrorMessage = "رمز عبور و تکرار آن یکسان نیستند")]
    [DataType(DataType.Password)]
    [Display(Name = "تکرار رمز عبور")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class RegisterReaderViewModel
{
    [Required(ErrorMessage = "auth.val.username_required")]
    [MaxLength(50, ErrorMessage = "auth.val.username_max")]
    [Display(Name = "Username")]
    public string UserName { get; set; } = string.Empty;

    [ValidEmail]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "auth.val.display_required")]
    [MaxLength(100, ErrorMessage = "auth.val.display_max")]
    [Display(Name = "Display name")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "auth.val.password_required")]
    [MinLength(10, ErrorMessage = "auth.val.password_min")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "auth.val.confirm_required")]
    [Compare(nameof(Password), ErrorMessage = "auth.val.confirm_mismatch")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class AuthorListItem
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsSuperAdmin { get; set; }
    public int PostCount { get; set; }
    public bool HasProfileImage { get; set; }
}

public class PublicAuthorProfileViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public bool HasProfileImage { get; set; }
    public UserGender Gender { get; set; }
    public string? Twitter { get; set; }
    public string? LinkedIn { get; set; }
    public string? Telegram { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }
    public string? GitHub { get; set; }
    public string? Instagram { get; set; }
    public bool IsFollowing { get; set; }
    public bool CanFollow { get; set; }
    public bool IsOwnProfile { get; set; }
    public bool IsAuthor { get; set; }
    public bool IsSuperAdmin { get; set; }
    public DateTime JoinedAtUtc { get; set; }
    public int FollowerCount { get; set; }
    public int PostCount { get; set; }
    public long TotalViews { get; set; }
    public List<AuthorPostItem> Posts { get; set; } = new();

    // Filters
    public string? Q { get; set; }
    public string Sort { get; set; } = "newest";
    public string? Folder { get; set; }
    public string? Category { get; set; }
    public string? Tag { get; set; }
    public string? Series { get; set; }
    public string? Topic { get; set; }
    public int FilteredCount { get; set; }

    public List<AuthorFilterOption> Folders { get; set; } = new();
    public List<AuthorFilterOption> Categories { get; set; } = new();
    public List<AuthorFilterOption> Tags { get; set; } = new();
    public List<AuthorFilterOption> SeriesList { get; set; } = new();
    public List<AuthorFilterOption> Topics { get; set; } = new();

    public bool HasAnySocial =>
        !string.IsNullOrWhiteSpace(Twitter)
        || !string.IsNullOrWhiteSpace(LinkedIn)
        || !string.IsNullOrWhiteSpace(Telegram)
        || !string.IsNullOrWhiteSpace(Phone)
        || !string.IsNullOrWhiteSpace(Website)
        || !string.IsNullOrWhiteSpace(GitHub)
        || !string.IsNullOrWhiteSpace(Instagram);

    public bool HasActiveFilter =>
        !string.IsNullOrWhiteSpace(Q)
        || !string.IsNullOrWhiteSpace(Folder)
        || !string.IsNullOrWhiteSpace(Category)
        || !string.IsNullOrWhiteSpace(Tag)
        || !string.IsNullOrWhiteSpace(Series)
        || !string.IsNullOrWhiteSpace(Topic)
        || (!string.IsNullOrWhiteSpace(Sort) && Sort != "newest");

    /// <summary>GitHub-style publishing activity for the selected year.</summary>
    public AuthorContributionViewModel Contribution { get; set; } = new();
}

public class AuthorFilterOption
{
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class AuthorPostItem
{
    public string Title { get; set; } = string.Empty;
    public stringSlug { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = "fa";
    public string? Summary { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public int ViewCount { get; set; }
    public int ReadingTimeMinutes { get; set; }
    public string? CategoryName { get; set; }
    public string? CoverUrl { get; set; }
}

public class AuthorContributionViewModel
{
    public int SelectedYear { get; set; }
    public List<int> AvailableYears { get; set; } = new();
    public int TotalInYear { get; set; }
    public bool UsePersianCalendar { get; set; }
    public List<AuthorContributionMonthLabel> MonthLabels { get; set; } = new();
    public List<AuthorContributionDay> Days { get; set; } = new();
    public List<AuthorContributionActivityGroup> ActivityGroups { get; set; } = new();
}

public class AuthorContributionDay
{
    public DateTime Date { get; set; }
    public int Count { get; set; }
    public int Level { get; set; }
    public string Tooltip { get; set; } = string.Empty;
    public bool InSelectedYear { get; set; }
}

public class AuthorContributionMonthLabel
{
    public string Label { get; set; } = string.Empty;
    public int WeekIndex { get; set; }
}

public class AuthorContributionActivityGroup
{
    public string MonthTitle { get; set; } = string.Empty;
    public int SortKey { get; set; }
    public List<AuthorContributionActivityItem> Items { get; set; } = new();
}

public class AuthorContributionActivityItem
{
    public string Kind { get; set; } = "posts";
    public string Title { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public List<AuthorContributionPostLink> Posts { get; set; } = new();
}

public class AuthorContributionPostLink
{
    public string Title { get; set; } = string.Empty;
    public stringSlug { get; set; } = string.Empty;
    public DateTime PublishedAtUtc { get; set; }
}

public class TelegramInstantViewModel
{
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string HtmlBody { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorUserName { get; set; }
    public DateTime PublishedAtUtc { get; set; }
    public string CanonicalUrl { get; set; } = string.Empty;
    public string SiteName { get; set; } = "Blog";
    public string? CoverUrl { get; set; }
    public string LanguageCode { get; set; } = "fa";
    public int ReadingTimeMinutes { get; set; }
    public List<string> Tags { get; set; } = new();
}
