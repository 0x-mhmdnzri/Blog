using BlogApp.Data;
using BlogApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BlogApp.Services;

public interface IUiTranslator
{
    /// <summary>Resolve a UI string for the current culture. Falls back to default language, then the key.</summary>
    string this[string key] { get; }

    string T(string key, string? languageCode = null);

    Task InvalidateCacheAsync();

    Task EnsureSeedAsync(CancellationToken ct = default);
}

/// <summary>
/// Loads all UiTranslation rows into memory cache (per language dictionary).
/// Cookie/culture is read from ICultureService — preference persists across pages.
/// </summary>
public sealed class UiTranslatorService : IUiTranslator
{
    private const string CacheKeyPrefix = "ui-i18n:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);

    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ICultureService _culture;

    public UiTranslatorService(
        ApplicationDbContext db,
        IMemoryCache cache,
        ICultureService culture)
    {
        _db = db;
        _cache = cache;
        _culture = culture;
    }

    public string this[string key] => T(key);

    public string T(string key, string? languageCode = null)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;

        var lang = AppCultures.Normalize(languageCode ?? _culture.CurrentCode);
        var map = GetMap(lang);

        if (map.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
            return value;

        // Fallback to default language (fa)
        if (lang != AppCultures.Default)
        {
            var fallback = GetMap(AppCultures.Default);
            if (fallback.TryGetValue(key, out var fb) && !string.IsNullOrEmpty(fb))
                return fb;
        }

        // Last resort: humanize the key
        return key.Contains('.') ? key[(key.LastIndexOf('.') + 1)..] : key;
    }

    private IReadOnlyDictionary<string, string> GetMap(string lang)
    {
        return _cache.GetOrCreate(CacheKeyPrefix + lang, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            // Sync load is OK — small table, warm on first hit
            return _db.UiTranslations.AsNoTracking()
                .Where(t => t.LanguageCode == lang)
                .ToDictionary(t => t.Key, t => t.Value, StringComparer.OrdinalIgnoreCase);
        }) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public Task InvalidateCacheAsync()
    {
        foreach (var c in AppCultures.All)
            _cache.Remove(CacheKeyPrefix + c.Code);
        return Task.CompletedTask;
    }

    public async Task EnsureSeedAsync(CancellationToken ct = default)
    {
        var existing = await _db.UiTranslations.AsNoTracking()
            .Select(t => t.Key + "|" + t.LanguageCode)
            .ToListAsync(ct);
        var set = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        var rows = UiTranslationCatalog.All;
        var added = 0;
        foreach (var (key, group, fa, en, ar) in rows)
        {
            foreach (var (code, value) in new[] { ("fa", fa), ("en", en), ("ar", ar) })
            {
                var id = key + "|" + code;
                if (set.Contains(id)) continue;
                _db.UiTranslations.Add(new UiTranslation
                {
                    Key = key,
                    LanguageCode = code,
                    Value = value,
                    Group = group,
                    UpdatedAtUtc = DateTime.UtcNow
                });
                set.Add(id);
                added++;
            }
        }

        if (added > 0)
            await _db.SaveChangesAsync(ct);

        await InvalidateCacheAsync();
    }
}

/// <summary>Built-in seed catalog for UI chrome (not post content).</summary>
public static class UiTranslationCatalog
{
    // (key, group, fa, en, ar)
    public static readonly (string Key, string Group, string Fa, string En, string Ar)[] All =
    {
        // ---- Public nav ----
        ("nav.posts", "nav", "نوشته‌ها", "Posts", "المقالات"),
        ("nav.bookmarks", "nav", "نشان‌ها", "Bookmarks", "الإشارات"),
        ("nav.notifications", "nav", "اعلان‌ها", "Notifications", "الإشعارات"),
        ("nav.admin", "nav", "پنل مدیریت", "Admin", "الإدارة"),
        ("nav.login", "nav", "ورود", "Login", "دخول"),
        ("nav.register", "nav", "ثبت‌نام", "Register", "تسجيل"),
        ("nav.logout", "nav", "خروج", "Logout", "خروج"),
        ("nav.language", "nav", "زبان", "Language", "اللغة"),

        // ---- Common buttons ----
        ("btn.save", "button", "ذخیره", "Save", "حفظ"),
        ("btn.cancel", "button", "انصراف", "Cancel", "إلغاء"),
        ("btn.delete", "button", "حذف", "Delete", "حذف"),
        ("btn.edit", "button", "ویرایش", "Edit", "تعديل"),
        ("btn.create", "button", "ایجاد", "Create", "إنشاء"),
        ("btn.search", "button", "جست‌وجو", "Search", "بحث"),
        ("btn.filter", "button", "فیلتر", "Filter", "تصفية"),
        ("btn.submit", "button", "ارسال", "Submit", "إرسال"),
        ("btn.back", "button", "بازگشت", "Back", "رجوع"),
        ("btn.approve", "button", "تأیید", "Approve", "موافقة"),
        ("btn.reject", "button", "رد", "Reject", "رفض"),
        ("btn.resolve", "button", "حل", "Resolve", "حل"),
        ("btn.dismiss", "button", "رد کردن", "Dismiss", "تجاهل"),
        ("btn.duplicate", "button", "کپی", "Duplicate", "نسخ"),
        ("btn.soft_delete", "button", "حذف نرم", "Soft delete", "حذف ناعم"),
        ("btn.report", "button", "گزارش", "Report", "إبلاغ"),
        ("btn.bookmark", "button", "نشان", "Bookmark", "إشارة"),
        ("btn.bookmarked", "button", "نشان‌شده", "Bookmarked", "مُعلَّم"),
        ("btn.like", "button", "پسند", "Like", "إعجاب"),

        // ---- Home ----
        ("home.title", "page", "نوشته‌ها", "Posts", "المقالات"),
        ("home.hero_title", "page", "یادداشت‌هایی درباره ساختن چیزها.", "Notes on building things.", "ملاحظات حول بناء الأشياء."),
        ("home.hero_body", "page",
            "نوشته‌های بلند، دقیقاً به همان سبکی که یک ریدمی نوشته می‌شود — مارک‌داون به‌عنوان ورودی، خروجی شیک و استایل‌دار، همراه با کد، تصویر و ویدیو دقیقاً درون متن.",
            "Long-form writing in the style of a README — Markdown in, polished output with code, images and video inline.",
            "كتابات طويلة بأسلوب ملف README — ماركداون كمدخل، ومخرجات أنيقة مع الكود والصور والفيديو داخل النص."),
        ("home.all", "page", "همه", "All", "الكل"),
        ("home.empty", "page", "هنوز نوشته‌ای منتشر نشده — اولین نوشته منتظر نوشته‌شدن است.", "No posts published yet — the first one is waiting to be written.", "لا توجد مقالات منشورة بعد."),
        ("home.draft", "page", "پیش‌نویس", "Draft", "مسودة"),

        // ---- Admin shell ----
        ("admin.brand", "admin", "پنل مدیریت وبلاگ", "Blog Admin", "لوحة إدارة المدونة"),
        ("admin.panel", "admin", "پنل مدیریت", "Admin panel", "لوحة الإدارة"),
        ("admin.live", "admin", "زنده", "Live", "مباشر"),
        ("admin.live_title", "admin", "اتصال زنده به سرور", "Live connection to server", "اتصال مباشر بالخادم"),
        ("admin.back_to_site", "admin", "بازگشت به سایت", "Back to site", "العودة إلى الموقع"),
        ("admin.logout", "admin", "خروج از حساب", "Sign out", "تسجيل الخروج"),
        ("admin.demo", "admin", "دمو", "Demo", "تجريبي"),
        ("admin.super_only", "admin", "فقط ادمین", "Admin only", "للمدير فقط"),

        // ---- Admin nav groups ----
        ("admin.group.general", "admin", "کلی", "General", "عام"),
        ("admin.group.content", "admin", "محتوا", "Content", "المحتوى"),
        ("admin.group.growth", "admin", "رشد و سئو", "Growth & SEO", "النمو وتحسين محركات البحث"),
        ("admin.group.account", "admin", "حساب", "Account", "الحساب"),
        ("admin.group.system", "admin", "سیستم", "System", "النظام"),

        // ---- Admin nav links ----
        ("admin.nav.dashboard", "admin", "داشبورد", "Dashboard", "لوحة التحكم"),
        ("admin.nav.moderation", "admin", "صف بررسی", "Moderation queue", "قائمة المراجعة"),
        ("admin.nav.posts", "admin", "نوشته‌ها", "Posts", "المقالات"),
        ("admin.nav.comments", "admin", "دیدگاه‌ها", "Comments", "التعليقات"),
        ("admin.nav.reports", "admin", "گزارش‌ها", "Reports", "التقارير"),
        ("admin.nav.media", "admin", "رسانه‌ها", "Media", "الوسائط"),
        ("admin.nav.taxonomy", "admin", "دسته‌ها و برچسب‌ها", "Categories & tags", "التصنيفات والوسوم"),
        ("admin.nav.analytics", "admin", "آمار و تحلیل", "Analytics", "الإحصاءات"),
        ("admin.nav.seo", "admin", "ابزارهای سئو", "SEO tools", "أدوات تحسين محركات البحث"),
        ("admin.nav.newsletter", "admin", "خبرنامه", "Newsletter", "النشرة"),
        ("admin.nav.profile", "admin", "پروفایل من", "My profile", "ملفي"),
        ("admin.nav.users", "admin", "مدیریت کاربران", "User management", "إدارة المستخدمين"),
        ("admin.nav.authors", "admin", "نویسندگان", "Authors", "المؤلفون"),
        ("admin.nav.settings", "admin", "تنظیمات سایت", "Site settings", "إعدادات الموقع"),
        ("admin.nav.flags", "admin", "پرچم‌های ویژگی", "Feature flags", "أعلام الميزات"),
        ("admin.nav.audit", "admin", "حسابرسی", "Audit log", "سجل التدقيق"),
        ("admin.nav.translations", "admin", "ترجمه‌های رابط", "UI translations", "ترجمات الواجهة"),

        // ---- Post / comments chrome ----
        ("post.comments", "page", "دیدگاه‌ها", "Comments", "التعليقات"),
        ("post.sort", "page", "مرتب‌سازی", "Sort", "ترتيب"),
        ("post.sort_relevant", "page", "مرتبط (پسندها)", "Relevant (likes)", "الأكثر صلة"),
        ("post.sort_latest", "page", "جدیدترین", "Latest", "الأحدث"),
        ("post.reading_min", "page", "دقیقه مطالعه", "min read", "دقيقة قراءة"),
        ("post.views", "page", "بازدید", "views", "مشاهدة"),
        ("post.featured", "page", "ویژه", "Featured", "مميز"),
        ("post.sticky", "page", "چسبان", "Sticky", "مثبت"),
        ("post.comment_name", "form", "نام شما", "Your name", "اسمك"),
        ("post.comment_body", "form", "متن دیدگاه…", "Write a comment…", "اكتب تعليقاً…"),
        ("post.comment_send", "form", "ارسال دیدگاه", "Post comment", "نشر التعليق"),
        ("post.report_reason", "form", "دلیل گزارش", "Report reason", "سبب الإبلاغ"),
        ("post.report_send", "form", "ارسال گزارش", "Submit report", "إرسال البلاغ"),

        // ---- Auth ----
        ("auth.login", "auth", "ورود", "Login", "دخول"),
        ("auth.register", "auth", "ثبت‌نام", "Register", "تسجيل"),
        ("auth.email", "auth", "ایمیل", "Email", "البريد"),
        ("auth.password", "auth", "رمز عبور", "Password", "كلمة المرور"),
        ("auth.username", "auth", "نام کاربری", "Username", "اسم المستخدم"),

        // ---- Status ----
        ("status.open", "status", "باز", "Open", "مفتوح"),
        ("status.resolved", "status", "حل‌شده", "Resolved", "محلول"),
        ("status.dismissed", "status", "ردشده", "Dismissed", "مرفوض"),
        ("status.pending", "status", "در انتظار", "Pending", "قيد الانتظار"),
        ("status.approved", "status", "تأییدشده", "Approved", "موافق عليه"),
        ("status.rejected", "status", "ردشده", "Rejected", "مرفوض"),
        ("status.published", "status", "منتشر", "Published", "منشور"),
        ("status.draft", "status", "پیش‌نویس", "Draft", "مسودة"),
        ("status.active", "status", "فعال", "Active", "نشط"),
        ("status.locked", "status", "قفل", "Locked", "مقفل"),

        // ---- Messages ----
        ("msg.saved", "message", "ذخیره شد.", "Saved.", "تم الحفظ."),
        ("msg.empty", "message", "موردی یافت نشد.", "Nothing found.", "لا توجد عناصر."),
        ("msg.confirm_delete", "message", "آیا مطمئن هستید؟", "Are you sure?", "هل أنت متأكد؟"),
    };
}
