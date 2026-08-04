namespace BlogApp.Services;

/// <summary>Spotlight search (public + admin) — FA / EN / AR.</summary>
public static partial class UiTranslationCatalog
{
    public static readonly (string Key, string Group, string Fa, string En, string Ar)[] Search =
    {
        ("search.placeholder", "search", "جست‌وجو…", "Search…", "بحث…"),
        ("search.aria", "search", "جست‌وجو", "Search", "بحث"),
        ("search.label", "search", "جست‌وجوی نوشته‌ها", "Search posts", "البحث في المقالات"),
        ("search.clear", "search", "پاک کردن", "Clear", "مسح"),
        ("search.idle_hint", "search", "جست‌وجو در نوشته‌ها", "Search posts", "ابحث في المقالات"),
        ("search.idle_keys", "search", "↑↓ پیمایش · Enter باز کردن · Esc بستن", "↑↓ navigate · Enter open · Esc close", "↑↓ للتنقل · Enter للفتح · Esc للإغلاق"),
        ("search.nav", "search", "پیمایش", "Navigate", "تنقل"),
        ("search.open", "search", "باز کردن", "Open", "فتح"),
        ("search.close", "search", "بستن", "Close", "إغلاق"),
        ("search.results", "search", "نتیجه", "results", "نتيجة"),
        ("search.no_results", "search", "نتیجه‌ای یافت نشد", "No results found", "لا توجد نتائج"),
        ("search.recent", "search", "جست‌وجوهای اخیر", "Recent searches", "عمليات البحث الأخيرة"),
        ("search.all_results", "search", "همه نتایج", "All results", "كل النتائج"),
        ("search.loading", "search", "در حال جست‌وجو…", "Searching…", "جاري البحث…"),

        ("search.admin.placeholder", "search", "جست‌وجو در پنل…", "Search admin…", "بحث في لوحة التحكم…"),
        ("search.admin.aria", "search", "جست‌وجوی مدیریت", "Admin search", "بحث الإدارة"),
        ("search.admin.idle_hint", "search", "جست‌وجو در پنل مدیریت", "Search the admin panel", "ابحث في لوحة التحكم"),
        ("search.admin.idle_keys", "search", "↑↓ پیمایش · Enter باز کردن · Esc بستن", "↑↓ navigate · Enter open · Esc close", "↑↓ للتنقل · Enter للفتح · Esc للإغلاق"),
        ("search.admin.scope", "search", "محدوده", "Scope", "النطاق"),
        ("search.admin.scope_all", "search", "همه", "All", "الكل"),
        ("search.admin.scope_post", "search", "نوشته‌ها", "Posts", "المقالات"),
        ("search.admin.scope_comment", "search", "دیدگاه‌ها", "Comments", "التعليقات"),
        ("search.admin.scope_user", "search", "کاربران", "People", "الأشخاص"),
        ("search.admin.scope_media", "search", "رسانه", "Media", "الوسائط"),
        ("search.admin.scope_page", "search", "صفحات", "Pages", "الصفحات"),
        ("search.admin.scope_theme", "search", "تم‌ها", "Themes", "السمات"),
        ("search.admin.full", "search", "نتایج کامل", "Full results", "النتائج الكاملة"),
        ("search.admin.field", "search", "جست‌وجو", "Search", "بحث"),
    };
}
