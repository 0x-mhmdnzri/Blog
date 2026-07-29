using System.ComponentModel.DataAnnotations;
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

    public IFormFile? ProfileImageFile { get; set; }
    public bool RemoveProfileImage { get; set; }
    public bool HasProfileImage { get; set; }
}

public class CreateAuthorViewModel
{
    [Required, MaxLength(50)]
    public string UserName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Bio { get; set; }

    [Required, MinLength(10)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required, Compare(nameof(Password), ErrorMessage = "رمز عبور و تکرار آن یکسان نیستند")]
    [DataType(DataType.Password)]
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
    public bool IsFollowing { get; set; }
    public bool CanFollow { get; set; }
    public List<AuthorPostItem> Posts { get; set; } = new();
}

public class AuthorPostItem
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public int ViewCount { get; set; }
}
