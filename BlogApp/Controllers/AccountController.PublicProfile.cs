using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public partial class AccountController
{
    [HttpGet]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> PublicProfile(
        string userName,
        string? q = null,
        string? sort = null,
        string? folder = null,
        string? category = null,
        string? tag = null,
        string? series = null,
        string? topic = null,
        int? year = null)
    {
        if (string.IsNullOrWhiteSpace(userName) || userName.Length > 64)
            return NotFound();

        var user = await _userManager.FindByNameAsync(userName);
        if (user is null) return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        var isAuthor = roles.Contains(AppRoles.Author) || roles.Contains(AppRoles.SuperAdmin);
        var isSuper = roles.Contains(AppRoles.SuperAdmin);

        var baseQuery = _db.Posts.AsNoTracking()
            .Where(p => p.AuthorId == user.Id && p.IsPublished && !p.IsDeleted);

        var postCount = await baseQuery.CountAsync();
        var totalViews = await baseQuery.SumAsync(p => (long)p.ViewCount);
        var followerCount = await _db.AuthorFollows.CountAsync(f => f.AuthorUserId == user.Id);

        var folders = await _db.PostFolderItems.AsNoTracking()
            .Where(i => i.Post.AuthorId == user.Id && i.Post.IsPublished && !i.Post.IsDeleted)
            .GroupBy(i => new { i.Folder.Slug, i.Folder.Name })
            .Select(g => new AuthorFilterOption {Slug = g.Key.Slug, Name = g.Key.Name, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(40)
            .ToListAsync();

        var categories = await baseQuery
            .Where(p => p.Category != null)
            .GroupBy(p => new { p.Category!.Slug, p.Category.Name })
            .Select(g => new AuthorFilterOption {Slug = g.Key.Slug, Name = g.Key.Name, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(40)
            .ToListAsync();

        var tags = await _db.PostTags.AsNoTracking()
            .Where(pt => pt.Post.AuthorId == user.Id && pt.Post.IsPublished && !pt.Post.IsDeleted)
            .GroupBy(pt => new { pt.Tag.Slug, pt.Tag.Name })
            .Select(g => new AuthorFilterOption {Slug = g.Key.Slug, Name = g.Key.Name, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(40)
            .ToListAsync();

        var seriesList = await _db.SeriesPosts.AsNoTracking()
            .Where(sp => sp.Post.AuthorId == user.Id && sp.Post.IsPublished && !sp.Post.IsDeleted)
            .GroupBy(sp => new { sp.Series.Slug, sp.Series.Name })
            .Select(g => new AuthorFilterOption {Slug = g.Key.Slug, Name = g.Key.Name, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(40)
            .ToListAsync();

        var topics = await _db.TopicCollections.AsNoTracking()
            .Where(t => t.IsPublished)
            .OrderBy(t => t.Name)
            .Select(t => new AuthorFilterOption {Slug = t.Slug, Name = t.Name, Count = t.Items.Count })
            .Take(40)
            .ToListAsync();

        var query = baseQuery;

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(p =>
                p.Title.Contains(term)
                || (p.Summary != null && p.Summary.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(folder))
        {
            var fs = folder.Trim();
            query = query.Where(p =>
                _db.PostFolderItems.Any(i => i.PostId == p.Id && i.Folder.Slug == fs));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var cs = category.Trim();
            query = query.Where(p => p.Category != null && p.Category.Slug == cs);
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            var ts = tag.Trim();
            query = query.Where(p => p.PostTags.Any(pt => pt.Tag.Slug == ts));
        }

        if (!string.IsNullOrWhiteSpace(series))
        {
            var ss = series.Trim();
            query = query.Where(p =>
                p.SeriesMemberships.Any(sp => sp.Series.Slug == ss));
        }

        if (!string.IsNullOrWhiteSpace(topic))
        {
            var tslug = topic.Trim();
            var topicRow = await _db.TopicCollections.AsNoTracking()
                .Include(t => t.Items)
                .FirstOrDefaultAsync(t => t.Slug == tslug);
            if (topicRow != null)
            {
                var catIds = topicRow.Items.Where(i => i.CategoryId != null).Select(i => i.CategoryId!.Value).ToList();
                var tagIds = topicRow.Items.Where(i => i.TagId != null).Select(i => i.TagId!.Value).ToList();
                if (catIds.Count > 0 || tagIds.Count > 0)
                {
                    query = query.Where(p =>
                        (p.CategoryId != null && catIds.Contains(p.CategoryId.Value))
                        || p.PostTags.Any(pt => tagIds.Contains(pt.TagId)));
                }
                else
                {
                    query = query.Where(_ => false);
                }
            }
        }

        sort = string.IsNullOrWhiteSpace(sort) ? "newest" : sort.Trim().ToLowerInvariant();
        query = sort switch
        {
            "oldest" => query.OrderBy(p => p.PublishedAtUtc),
            "popular" => query.OrderByDescending(p => p.ViewCount).ThenByDescending(p => p.PublishedAtUtc),
            "title" => query.OrderBy(p => p.Title),
            "read" => query.OrderByDescending(p => p.ReadingTimeMinutes),
            _ => query.OrderByDescending(p => p.IsSticky).ThenByDescending(p => p.PublishedAtUtc)
        };

        var filteredCount = await query.CountAsync();
        var posts = await query
            .Take(60)
            .Select(p => new AuthorPostItem
            {
                Title = p.Title,
                Slug = p.Slug,
                LanguageCode = p.LanguageCode,
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

        // Publishing activity (GitHub-style contribution graph)
        var publishDates = await baseQuery
            .Where(p => p.PublishedAtUtc != null)
            .Select(p => new { p.Title, p.Slug, Date = p.PublishedAtUtc!.Value })
            .ToListAsync();

        var cultureSvc = HttpContext.RequestServices.GetService(typeof(ICultureService)) as ICultureService;
        var cultureCode = cultureSvc?.CurrentCode ?? "fa";
        var contribution = BuildAuthorContribution(
            publishDates.Select(x => (x.Title, x.Slug, x.Date)).ToList(), year, cultureCode);

        var display = string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName! : user.DisplayName;
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var profileUrl = $"{baseUrl}/author/{Uri.EscapeDataString(user.UserName!)}";
        var ogImage = $"{baseUrl}/og/author/{user.Id}.png?v={postCount}-{followerCount}-{totalViews}";

        var desc = !string.IsNullOrWhiteSpace(user.Bio)
            ? user.Bio.Trim()
            : $"{display} (@{user.UserName}) — {postCount} posts, {followerCount} followers";
        if (desc.Length > 160) desc = desc[..157].TrimEnd() + "…";

        var keywordBits = new List<string> { display, user.UserName! };
        if (isAuthor) keywordBits.Add("author");
        keywordBits.AddRange(categories.Take(5).Select(c => c.Name));
        keywordBits.AddRange(tags.Take(8).Select(t => t.Name));

        ViewData["Title"] = postCount > 0
            ? $"{display} — {postCount} posts"
            : display;
        ViewData["Description"] = desc;
        ViewData["Keywords"] = string.Join(", ", keywordBits.Distinct(StringComparer.OrdinalIgnoreCase).Take(16));
        ViewData["Canonical"] = profileUrl;
        ViewData["OgType"] = "profile";
        ViewData["OgImage"] = ogImage;
        ViewData["OgImageAlt"] = display;
        ViewData["Author"] = display;
        ViewData["NoIndex"] = false;

        var seo = HttpContext.RequestServices.GetService(typeof(SeoService)) as SeoService;
        if (seo is not null)
        {
            ViewBag.PersonJsonLd = seo.BuildPersonJsonLd(baseUrl, user, profileUrl, postCount, ogImage);
        }

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
            Posts = posts,
            Q = q,
            Sort = sort,
            Folder = folder,
            Category = category,
            Tag = tag,
            Series = series,
            Topic = topic,
            FilteredCount = filteredCount,
            Folders = folders,
            Categories = categories,
            Tags = tags,
            SeriesList = seriesList,
            Topics = topics,
            Contribution = contribution
        };
        return View(vm);
    }

    private static AuthorContributionViewModel BuildAuthorContribution(
        List<(string Title, string Slug, DateTime Date)> posts,
        int? yearParam,
        string cultureCode)
    {
        var usePersian = string.Equals(cultureCode, "fa", StringComparison.OrdinalIgnoreCase);
        var pc = usePersian ? new System.Globalization.PersianCalendar() : null;
        var today = DateTime.UtcNow.Date;

        int YearOf(DateTime d) => usePersian ? pc!.GetYear(d) : d.Year;
        int MonthOf(DateTime d) => usePersian ? pc!.GetMonth(d) : d.Month;
        int DayOf(DateTime d) => usePersian ? pc!.GetDayOfMonth(d) : d.Day;

        string[] faMonths = { "", "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند" };
        string[] enMonths = { "", "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        string MonthName(int m) =>
            m >= 1 && m <= 12
                ? (usePersian ? faMonths[m] : enMonths[m])
                : "";

        var years = posts.Select(p => YearOf(p.Date)).Distinct().OrderByDescending(y => y).ToList();
        var currentYear = YearOf(today);
        if (!years.Contains(currentYear))
            years.Insert(0, currentYear);
        years = years.Distinct().OrderByDescending(y => y).ToList();

        var selectedYear = yearParam is int yp && years.Contains(yp)
            ? yp
            : (years.Count > 0 ? years[0] : currentYear);

        DateTime yearStart, yearEndExclusive;
        if (usePersian)
        {
            yearStart = pc!.ToDateTime(selectedYear, 1, 1, 0, 0, 0, 0);
            yearEndExclusive = pc.ToDateTime(selectedYear + 1, 1, 1, 0, 0, 0, 0);
        }
        else
        {
            yearStart = new DateTime(selectedYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            yearEndExclusive = new DateTime(selectedYear + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        var gridStart = yearStart.Date;
        while (gridStart.DayOfWeek != DayOfWeek.Monday)
            gridStart = gridStart.AddDays(-1);

        var gridEnd = yearEndExclusive.Date.AddDays(-1);
        while (gridEnd.DayOfWeek != DayOfWeek.Sunday)
            gridEnd = gridEnd.AddDays(1);

        var counts = posts
            .GroupBy(p => p.Date.Date)
            .ToDictionary(g => g.Key, g => g.Count());

        static int LevelOf(int c) => c <= 0 ? 0 : c == 1 ? 1 : c <= 3 ? 2 : c <= 6 ? 3 : 4;

        var days = new List<AuthorContributionDay>();
        for (var d = gridStart; d <= gridEnd; d = d.AddDays(1))
        {
            counts.TryGetValue(d, out var c);
            var inYear = d >= yearStart.Date && d < yearEndExclusive.Date;
            var tipCount = inYear ? c : 0;
            var dateLabel = usePersian
                ? $"{DayOf(d)} {MonthName(MonthOf(d))} {YearOf(d)}"
                : d.ToString("MMM d, yyyy");
            days.Add(new AuthorContributionDay
            {
                Date = d,
                Count = inYear ? c : 0,
                Level = inYear ? LevelOf(c) : 0,
                InSelectedYear = inYear,
                Tooltip = tipCount == 0
                    ? (usePersian ? $"بدون نوشته در {dateLabel}" : $"No posts on {dateLabel}")
                    : (usePersian
                        ? $"{tipCount} نوشته در {dateLabel}"
                        : $"{tipCount} post{(tipCount == 1 ? "" : "s")} on {dateLabel}")
            });
        }

        var monthLabels = new List<AuthorContributionMonthLabel>();
        var weekCount = (days.Count + 6) / 7;
        int? lastMonth = null;
        for (var wi = 0; wi < weekCount; wi++)
        {
            DateTime? inYearDay = null;
            for (var r = 0; r < 7; r++)
            {
                var idx = wi * 7 + r;
                if (idx < days.Count && days[idx].InSelectedYear)
                {
                    inYearDay = days[idx].Date;
                    break;
                }
            }
            if (inYearDay is null) continue;
            var m = MonthOf(inYearDay.Value);
            if (lastMonth != m)
            {
                monthLabels.Add(new AuthorContributionMonthLabel
                {
                    Label = MonthName(m),
                    WeekIndex = wi
                });
                lastMonth = m;
            }
        }

        var totalInYear = posts.Count(p =>
            p.Date.Date >= yearStart.Date && p.Date.Date < yearEndExclusive.Date);

        var yearPosts = posts
            .Where(p => p.Date.Date >= yearStart.Date && p.Date.Date < yearEndExclusive.Date)
            .OrderByDescending(p => p.Date)
            .ToList();

        var groups = yearPosts
            .GroupBy(p => YearOf(p.Date) * 100 + MonthOf(p.Date))
            .OrderByDescending(g => g.Key)
            .Select(g =>
            {
                var first = g.First();
                var m = MonthOf(first.Date);
                var y = YearOf(first.Date);
                var list = g.ToList();
                return new AuthorContributionActivityGroup
                {
                    MonthTitle = $"{MonthName(m)} {y}",
                    SortKey = g.Key,
                    Items = new List<AuthorContributionActivityItem>
                    {
                        new AuthorContributionActivityItem
                        {
                            Kind = "posts",
                            Title = usePersian
                                ? $"منتشر کرد {list.Count} نوشته"
                                : $"Published {list.Count} post{(list.Count == 1 ? "" : "s")}",
                            Posts = list.Select(p => new AuthorContributionPostLink
                            {
                                Title = p.Title,
                                Slug = p.Slug,
                                PublishedAtUtc = p.Date
                            }).ToList()
                        }
                    }
                };
            })
            .ToList();

        return new AuthorContributionViewModel
        {
            SelectedYear = selectedYear,
            AvailableYears = years,
            TotalInYear = totalInYear,
            UsePersianCalendar = usePersian,
            MonthLabels = monthLabels,
            Days = days,
            ActivityGroups = groups
        };
    }
}
