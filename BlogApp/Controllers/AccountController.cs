using BlogApp.Data;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public partial class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AccountController> _logger;
    private readonly IConfiguration _config;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext db,
        ILogger<AccountController> logger,
        IConfiguration config)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
        _logger = logger;
        _config = config;
    }

    [HttpGet]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        returnUrl = SanitizeReturnUrl(returnUrl);
        if (User.Identity?.IsAuthenticated == true)
            return await RedirectAfterLoginAsync(returnUrl);

        ViewBag.ReturnUrl = returnUrl;
        ViewBag.GoogleLoginEnabled = !string.IsNullOrWhiteSpace(_config["Authentication:Google:ClientId"]);
        ViewBag.GitHubLoginEnabled = !string.IsNullOrWhiteSpace(_config["Authentication:GitHub:ClientId"]);
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
    {
        returnUrl = SanitizeReturnUrl(returnUrl);

        if (User.Identity?.IsAuthenticated == true)
            return await RedirectAfterLoginAsync(returnUrl);

        username = (username ?? string.Empty).Trim();
        ViewBag.GoogleLoginEnabled = !string.IsNullOrWhiteSpace(_config["Authentication:Google:ClientId"]);
        ViewBag.GitHubLoginEnabled = !string.IsNullOrWhiteSpace(_config["Authentication:GitHub:ClientId"]);

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || password.Length > 256)
        {
            ModelState.AddModelError(string.Empty, "نام کاربری یا رمز عبور نادرست است.");
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        var user = await _userManager.FindByNameAsync(username)
                   ?? await _userManager.FindByEmailAsync(username);

        if (user is not null)
        {
            var result = await _signInManager.PasswordSignInAsync(
                user, password, isPersistent: true, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _logger.LogInformation("Login succeeded UserId={UserId} UserName={UserName}", user.Id, user.UserName);
                if (!string.IsNullOrEmpty(returnUrl))
                    return Redirect(returnUrl);

                if (await _userManager.IsInRoleAsync(user, AppRoles.Author)
                    || await _userManager.IsInRoleAsync(user, AppRoles.SuperAdmin))
                    return RedirectToAction("Index", "Admin");

                return RedirectToAction("Index", "Bookmarks");
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("Login locked out UserName={UserName}", username);
                ModelState.AddModelError(string.Empty, "حساب موقتاً قفل شده است. کمی بعد دوباره تلاش کنید.");
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }

            _logger.LogWarning("Login failed bad password UserName={UserName}", username);
        }
        else
        {
            _logger.LogWarning("Login failed unknown user UserName={UserName}", username);
            await Task.Delay(Random.Shared.Next(80, 180));
        }

        ModelState.AddModelError(string.Empty, "نام کاربری یا رمز عبور نادرست است.");
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpGet]
    [IgnoreAntiforgeryToken]
    public IActionResult Register(string? returnUrl = null)
    {
        return View(new RegisterReaderViewModel { ReturnUrl = SanitizeReturnUrl(returnUrl) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Register(RegisterReaderViewModel vm)
    {
        vm.ReturnUrl = SanitizeReturnUrl(vm.ReturnUrl);
        if (!ModelState.IsValid) return View(vm);

        var user = new ApplicationUser
        {
            UserName = vm.UserName.Trim(),
            Email = vm.Email.Trim(),
            EmailConfirmed = true,
            DisplayName = vm.DisplayName.Trim()
        };

        var result = await _userManager.CreateAsync(user, vm.Password);
        if (!result.Succeeded)
        {
            foreach (var err in result.Errors)
                ModelState.AddModelError(string.Empty, err.Description);
            return View(vm);
        }

        await _userManager.AddToRoleAsync(user, AppRoles.Reader);
        await _signInManager.SignInAsync(user, isPersistent: true);
        _logger.LogInformation("Reader registered UserId={UserId} UserName={UserName}", user.Id, user.UserName);

        if (!string.IsNullOrEmpty(vm.ReturnUrl))
            return Redirect(vm.ReturnUrl);

        return RedirectToAction("Index", "Bookmarks");
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
    [IgnoreAntiforgeryToken]
    public IActionResult AccessDenied()
    {
        _logger.LogWarning("Access denied User={User} Path={Path}", User.Identity?.Name, Request.Path.Value);
        return View();
    }

    [HttpGet]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> PublicProfile(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName) || userName.Length > 64)
            return NotFound();

        var user = await _userManager.FindByNameAsync(userName);
        if (user is null) return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        var isAuthor = roles.Contains(AppRoles.Author) || roles.Contains(AppRoles.SuperAdmin);
        var isSuper = roles.Contains(AppRoles.SuperAdmin);

        var postsQuery = _db.Posts
            .AsNoTracking()
            .Where(p => p.AuthorId == user.Id && p.IsPublished && !p.IsDeleted);

        var postCount = await postsQuery.CountAsync();
        var totalViews = await postsQuery.SumAsync(p => (long)p.ViewCount);
        var followerCount = await _db.AuthorFollows.CountAsync(f => f.AuthorUserId == user.Id);

        var posts = await postsQuery
            .OrderByDescending(p => p.IsSticky)
            .ThenByDescending(p => p.PublishedAtUtc)
            .Select(p => new AuthorPostItem
            {
                Title = p.Title,
                Slug = p.Slug,
                Summary = p.Summary,
                PublishedAtUtc = p.PublishedAtUtc,
                ViewCount = p.ViewCount,
                ReadingTimeMinutes = p.ReadingTimeMinutes,
                CategoryName = p.Category != null ? p.Category.Name : null,
                CoverUrl = p.CoverMediaAssetId != null ? "/media/" + p.CoverMediaAssetId : null
            })
            .ToListAsync();

        var viewerId = AuthorAccess.UserId(User);
        var isOwn = viewerId != null && viewerId == user.Id;
        var canFollow = viewerId != null && !isOwn
            && (User.IsInRole(AppRoles.Reader) || User.IsInRole(AppRoles.Author) || User.IsInRole(AppRoles.SuperAdmin));
        var isFollowing = canFollow
            && await _db.AuthorFollows.AnyAsync(f => f.FollowerUserId == viewerId && f.AuthorUserId == user.Id);

        ViewData["Description"] = string.IsNullOrWhiteSpace(user.Bio)
            ? $"{user.DisplayName} · @{user.UserName}"
            : user.Bio;
        ViewData["OgType"] = "profile";

        var vm = new PublicAuthorProfileViewModel
        {
            UserId = user.Id,
            UserName = user.UserName!,
            DisplayName = user.DisplayName,
            Bio = user.Bio,
            HasProfileImage = user.ProfileImage is { Length: > 0 },
            Gender = user.Gender,
            Twitter = user.Twitter,
            LinkedIn = user.LinkedIn,
            Telegram = user.Telegram,
            Phone = user.Phone,
            Website = user.Website,
            GitHub = user.GitHub,
            Instagram = user.Instagram,
            CanFollow = canFollow,
            IsFollowing = isFollowing,
            IsOwnProfile = isOwn,
            IsAuthor = isAuthor,
            IsSuperAdmin = isSuper,
            JoinedAtUtc = user.CreatedAtUtc,
            FollowerCount = followerCount,
            PostCount = postCount,
            TotalViews = totalViews,
            Posts = posts
        };
        return View(vm);
    }

    [HttpGet]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ProfileImage(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || userId.Length > 64)
            return NotFound();

        var user = await _userManager.FindByIdAsync(userId);
        if (user?.ProfileImage is null || user.ProfileImage.Length == 0)
            return NotFound();

        var ct = user.ProfileImageContentType ?? "image/png";
        if (ct.Contains("svg", StringComparison.OrdinalIgnoreCase)
            || ct.Contains("html", StringComparison.OrdinalIgnoreCase)
            || ct.Contains("javascript", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(user.ProfileImage, ct);
    }

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin + "," + AppRoles.Reader)]
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
            Gender = user.Gender,
            Twitter = user.Twitter,
            LinkedIn = user.LinkedIn,
            Telegram = user.Telegram,
            Phone = user.Phone,
            Website = user.Website,
            GitHub = user.GitHub,
            Instagram = user.Instagram,
            HasProfileImage = user.ProfileImage is { Length: > 0 }
        };
        return View(vm);
    }

    [Authorize(Roles = AppRoles.Author + "," + AppRoles.SuperAdmin + "," + AppRoles.Reader)]
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
        user.Gender = vm.Gender;
        ApplySocials(user, vm.Twitter, vm.LinkedIn, vm.Telegram, vm.Phone, vm.Website, vm.GitHub, vm.Instagram);

        if (!string.IsNullOrWhiteSpace(vm.Email) && vm.Email != user.Email)
        {
            user.Email = vm.Email.Trim();
            user.NormalizedEmail = _userManager.NormalizeEmail(vm.Email);
        }

        if (vm.ProfileImageFile is { Length: > 0 })
        {
            if (!await TrySetProfileImageAsync(user, vm.ProfileImageFile))
            {
                vm.HasProfileImage = user.ProfileImage is { Length: > 0 };
                return View(vm);
            }
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
            return View(vm);
        }

        await _signInManager.RefreshSignInAsync(user);
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
            Bio = string.IsNullOrWhiteSpace(vm.Bio) ? null : vm.Bio.Trim(),
            Gender = vm.Gender
        };
        ApplySocials(user, vm.Twitter, vm.LinkedIn, vm.Telegram, vm.Phone, vm.Website, vm.GitHub, vm.Instagram);

        if (vm.ProfileImageFile is { Length: > 0 })
        {
            if (!await TrySetProfileImageAsync(user, vm.ProfileImageFile))
                return View(vm);
        }

        var result = await _userManager.CreateAsync(user, vm.Password);
        if (!result.Succeeded)
        {
            foreach (var err in result.Errors)
                ModelState.AddModelError(string.Empty, err.Description);
            return View(vm);
        }

        await _userManager.AddToRoleAsync(user, AppRoles.Author);
        await _userManager.AddToRoleAsync(user, AppRoles.Reader);
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

    private async Task<bool> TrySetProfileImageAsync(ApplicationUser user, IFormFile file)
    {
        var check = SafeUpload.Validate(file);
        if (!check.Ok || check.Kind != MediaKind.Image)
        {
            ModelState.AddModelError("ProfileImageFile", check.Error ?? "تصویر نامعتبر است.");
            return false;
        }

        if (file.Length > 2 * 1024 * 1024)
        {
            ModelState.AddModelError("ProfileImageFile", "حداکثر اندازه تصویر پروفایل ۲ مگابایت است.");
            return false;
        }

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        user.ProfileImage = ms.ToArray();
        user.ProfileImageContentType = check.ContentType;
        return true;
    }

    private static void ApplySocials(
        ApplicationUser user,
        string? twitter, string? linkedIn, string? telegram,
        string? phone, string? website, string? github, string? instagram)
    {
        user.Twitter = NormalizeHandle(twitter);
        user.LinkedIn = NormalizeUrlOrHandle(linkedIn);
        user.Telegram = NormalizeHandle(telegram);
        user.Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        user.Website = NormalizeUrlOrHandle(website);
        user.GitHub = NormalizeHandle(github);
        user.Instagram = NormalizeHandle(instagram);
    }

    private static string? NormalizeHandle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        if (v.StartsWith('@')) v = v[1..];
        if (v.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || v.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(v);
                var seg = uri.AbsolutePath.Trim('/');
                if (!string.IsNullOrEmpty(seg))
                    v = seg.Split('/')[0];
            }
            catch { /* keep as-is */ }
        }
        return string.IsNullOrWhiteSpace(v) ? null : v[..Math.Min(v.Length, 120)];
    }

    private static string? NormalizeUrlOrHandle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        return v.Length > 200 ? v[..200] : v;
    }

    private async Task<IActionResult> RedirectAfterLoginAsync(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl))
            return Redirect(returnUrl);

        var user = await _userManager.GetUserAsync(User);
        if (user is not null)
        {
            if (await _userManager.IsInRoleAsync(user, AppRoles.Author)
                || await _userManager.IsInRoleAsync(user, AppRoles.SuperAdmin))
                return RedirectToAction("Index", "Admin");
        }

        return RedirectToAction("Index", "Bookmarks");
    }

    private string? SanitizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)) return null;
        if (!Url.IsLocalUrl(returnUrl)) return null;
        if (returnUrl.StartsWith("//", StringComparison.Ordinal)) return null;
        if (returnUrl.Contains('\\')) return null;
        return returnUrl;
    }
}
