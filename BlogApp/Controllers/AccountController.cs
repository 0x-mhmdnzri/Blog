using System.Security.Claims;
using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext db,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
    {
        var user = await _userManager.FindByNameAsync(username)
                   ?? await _userManager.FindByEmailAsync(username);

        if (user is not null)
        {
            var result = await _signInManager.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: false);
            if (result.Succeeded)
            {
                _logger.LogInformation("Login succeeded UserId={UserId} UserName={UserName}", user.Id, user.UserName);
                return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                    ? Redirect(returnUrl)
                    : RedirectToAction("Index", "Admin");
            }

            _logger.LogWarning("Login failed bad password UserName={UserName}", username);
        }
        else
        {
            _logger.LogWarning("Login failed unknown user UserName={UserName}", username);
        }

        ModelState.AddModelError(string.Empty, "نام کاربری یا رمز عبور نادرست است.");
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var name = User.Identity?.Name;
        await _signInManager.SignOutAsync();
        _logger.LogInformation("Logout UserName={UserName}", name);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        _logger.LogWarning("Access denied User={User} Path={Path}", User.Identity?.Name, Request.Path.Value);
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> PublicProfile(string userName)
    {
        var user = await _userManager.FindByNameAsync(userName);
        if (user is null) return NotFound();

        var posts = await _db.Posts
            .Where(p => p.AuthorId == user.Id && p.IsPublished)
            .OrderByDescending(p => p.PublishedAtUtc)
            .Select(p => new { p.Title, p.Slug, p.Summary, p.PublishedAtUtc, p.ViewCount })
            .ToListAsync();

        var vm = new PublicAuthorProfileViewModel
        {
            UserName = user.UserName!,
            DisplayName = user.DisplayName,
            Bio = user.Bio,
            HasProfileImage = user.ProfileImage is { Length: > 0 },
            Posts = posts.Select(p => new AuthorPostItem
            {
                Title = p.Title,
                Slug = p.Slug,
                Summary = p.Summary,
                PublishedAtUtc = p.PublishedAtUtc,
                ViewCount = p.ViewCount
            }).ToList()
        };
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> ProfileImage(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user?.ProfileImage is null || user.ProfileImage.Length == 0)
            return NotFound();

        return File(user.ProfileImage, user.ProfileImageContentType ?? "image/png");
    }

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var vm = new ProfileEditViewModel
        {
            DisplayName = user.DisplayName,
            Bio = user.Bio,
            Email = user.Email,
            HasProfileImage = user.ProfileImage is { Length: > 0 }
        };
        return View(vm);
    }

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileEditViewModel vm)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        if (!ModelState.IsValid)
        {
            vm.HasProfileImage = user.ProfileImage is { Length: > 0 };
            return View(vm);
        }

        user.DisplayName = vm.DisplayName.Trim();
        user.Bio = string.IsNullOrWhiteSpace(vm.Bio) ? null : vm.Bio.Trim();

        if (!string.IsNullOrWhiteSpace(vm.Email) && vm.Email != user.Email)
        {
            user.Email = vm.Email.Trim();
            user.NormalizedEmail = _userManager.NormalizeEmail(vm.Email);
        }

        if (vm.ProfileImageFile is { Length: > 0 })
        {
            if (vm.ProfileImageFile.Length > 2 * 1024 * 1024)
            {
                ModelState.AddModelError(nameof(vm.ProfileImageFile), "حداکثر اندازه تصویر پروفایل ۲ مگابایت است.");
                vm.HasProfileImage = user.ProfileImage is { Length: > 0 };
                return View(vm);
            }

            using var ms = new MemoryStream();
            await vm.ProfileImageFile.CopyToAsync(ms);
            user.ProfileImage = ms.ToArray();
            user.ProfileImageContentType = vm.ProfileImageFile.ContentType;
        }

        if (vm.RemoveProfileImage)
        {
            user.ProfileImage = null;
            user.ProfileImageContentType = null;
        }

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var err in result.Errors)
                ModelState.AddModelError(string.Empty, err.Description);
            vm.HasProfileImage = user.ProfileImage is { Length: > 0 };
            _logger.LogWarning("Profile update failed UserId={UserId}", user.Id);
            return View(vm);
        }

        await _signInManager.RefreshSignInAsync(user);
        _logger.LogInformation("Profile updated UserId={UserId}", user.Id);

        TempData["ProfileSaved"] = "پروفایل با موفقیت ذخیره شد.";
        return RedirectToAction(nameof(Profile));
    }

    [Authorize(Roles = AppRoles.SuperAdmin)]
    [HttpGet]
    public IActionResult CreateAuthor() => View(new CreateAuthorViewModel());

    [Authorize(Roles = AppRoles.SuperAdmin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAuthor(CreateAuthorViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = new ApplicationUser
        {
            UserName = vm.UserName.Trim(),
            Email = vm.Email.Trim(),
            EmailConfirmed = true,
            DisplayName = vm.DisplayName.Trim(),
            Bio = string.IsNullOrWhiteSpace(vm.Bio) ? null : vm.Bio.Trim()
        };

        var result = await _userManager.CreateAsync(user, vm.Password);
        if (!result.Succeeded)
        {
            foreach (var err in result.Errors)
                ModelState.AddModelError(string.Empty, err.Description);
            _logger.LogWarning("CreateAuthor failed UserName={UserName}", vm.UserName);
            return View(vm);
        }

        await _userManager.AddToRoleAsync(user, AppRoles.Author);
        _logger.LogInformation("Author created UserId={UserId} UserName={UserName}", user.Id, user.UserName);

        TempData["AuthorCreated"] = $"نویسنده «{user.DisplayName}» ایجاد شد.";
        return RedirectToAction(nameof(Authors));
    }

    [Authorize(Roles = AppRoles.SuperAdmin)]
    [HttpGet]
    public async Task<IActionResult> Authors()
    {
        var authors = await _userManager.GetUsersInRoleAsync(AppRoles.Author);
        var items = new List<AuthorListItem>();
        foreach (var a in authors.OrderBy(a => a.DisplayName))
        {
            var roles = await _userManager.GetRolesAsync(a);
            var postCount = await _db.Posts.CountAsync(p => p.AuthorId == a.Id);
            items.Add(new AuthorListItem
            {
                Id = a.Id,
                UserName = a.UserName!,
                DisplayName = a.DisplayName,
                Email = a.Email,
                IsSuperAdmin = roles.Contains(AppRoles.SuperAdmin),
                PostCount = postCount,
                HasProfileImage = a.ProfileImage is { Length: > 0 }
            });
        }
        return View(items);
    }
}
