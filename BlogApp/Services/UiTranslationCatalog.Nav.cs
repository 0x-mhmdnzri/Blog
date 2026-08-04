namespace BlogApp.Services;

public static partial class UiTranslationCatalog
{
    /// <summary>Client navbar + drawer + account menu (FA / EN / AR).</summary>
    public static readonly (string Key, string Group, string Fa, string En, string Ar)[] NavExtra =
    {
        ("nav.main", "nav", "ناوبری اصلی", "Main navigation", "التنقل الرئيسي"),
        ("nav.menu", "nav", "منو", "Menu", "القائمة"),
        ("nav.menu_open", "nav", "باز کردن منو", "Open menu", "فتح القائمة"),
        ("nav.menu_close", "nav", "بستن منو", "Close menu", "إغلاق القائمة"),
        ("nav.pages", "nav", "صفحات", "Pages", "الصفحات"),
        ("nav.account", "nav", "حساب", "Account", "الحساب"),
        ("nav.account_tools", "nav", "حساب و ابزار", "Account & tools", "الحساب والأدوات"),
        ("nav.section_pages", "nav", "صفحات", "Pages", "الصفحات"),
        ("nav.section_account", "nav", "حساب", "Account", "الحساب"),
        ("nav.public_profile", "nav", "پروفایل عمومی", "Public profile", "الملف العام"),
        ("nav.account_settings", "nav", "تنظیمات حساب", "Account settings", "إعدادات الحساب"),
        ("nav.api_keys", "nav", "کلیدهای API", "API keys", "مفاتيح API"),
        ("nav.themes", "nav", "تم‌ها", "Themes", "السمات"),
        ("nav.activity_feed", "nav", "فید فعالیت", "Activity feed", "سجل النشاط"),
        ("nav.profile", "nav", "پروفایل", "Profile", "الملف"),
        ("nav.search", "nav", "جست‌وجو", "Search", "بحث"),
    };
}
