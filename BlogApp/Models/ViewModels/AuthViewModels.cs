using System.ComponentModel.DataAnnotations;
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
    [Required(ErrorMessage = "Username is required")]
    [MaxLength(50, ErrorMessage = "Max 50 characters")]
    [RegularExpression(@"^[a-zA-Z0-9._-]{3,50}$",
        ErrorMessage = "3–50 chars: letters, numbers, . _ -")]
    [Display(Name = "Username")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    [MaxLength(256)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Display name is required")]
    [MaxLength(100, ErrorMessage = "Max 100 characters")]
    [MinLength(2, ErrorMessage = "At least 2 characters")]
    [Display(Name = "Display name")]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Max 500 characters")]
    [Display(Name = "Bio")]
    public string? Bio { get; set; }

    [Display(Name = "Gender")]
    public UserGender Gender { get; set; } = UserGender.Unspecified;

    [MaxLength(120)]
    public string? Twitter { get; set; }

    [MaxLength(200)]
    public string? LinkedIn { get; set; }

    [MaxLength(120)]
    public string? Telegram { get; set; }

    [MaxLength(40)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Website { get; set; }

    [MaxLength(120)]
    public string? GitHub { get; set; }

    [MaxLength(120)]
    public string? Instagram { get; set; }

    public IFormFile? ProfileImageFile { get; set; }

    [Required(ErrorMessage = "Password is required")]
    [MinLength(10, ErrorMessage = "At least 10 characters")]
    [MaxLength(128)]
    [DataType(DataType.Password)]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{10,}$",
        ErrorMessage = "Need upper, lower, and a digit (min 10)")]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm password is required")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class RegisterReaderViewModel
{
    [Required(ErrorMessage = "نام کاربری الزامی است"), MaxLength(50)]
    [Display(Name = "نام کاربری")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "ایمیل الزامی است"), EmailAddress]
    [Display(Name = "ایمیل")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "نام نمایشی الزامی است"), MaxLength(100)]
    [Display(Name = "نام نمایشی")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "رمز عبور الزامی است"), MinLength(10, ErrorMessage = "حداقل ۱۰ نویسه")]
    [DataType(DataType.Password)]
    [Display(Name = "رمز عبور")]
    public string Password { get; set; } = string.Empty;

    [Required, Compare(nameof(Password), ErrorMessage = "رمز عبور و تکرار آن یکسان نیستند")]
    [DataType(DataType.Password)]
    [Display(Name = "تکرار رمز عبور")]
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

    public bool HasAnySocial =>
        !string.IsNullOrWhiteSpace(Twitter)
        || !string.IsNullOrWhiteSpace(LinkedIn)
        || !string.IsNullOrWhiteSpace(Telegram)
        || !string.IsNullOrWhiteSpace(Phone)
        || !string.IsNullOrWhiteSpace(Website)
        || !string.IsNullOrWhiteSpace(GitHub)
        || !string.IsNullOrWhiteSpace(Instagram);
}

public class AuthorPostItem
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public int ViewCount { get; set; }
    public int ReadingTimeMinutes { get; set; }
    public string? CategoryName { get; set; }
    public string? CoverUrl { get; set; }
}
