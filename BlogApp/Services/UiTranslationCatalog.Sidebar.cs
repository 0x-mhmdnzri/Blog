namespace BlogApp.Services;

public static partial class UiTranslationCatalog
{
    public static readonly (string Key, string Group, string Fa, string En, string Ar)[] Sidebar =
    {
        ("admin.sidebar_lock", "admin", "قفل سایدبار (نمایش کامل)", "Lock sidebar (full labels)", "تثبيت الشريط (تسميات كاملة)"),
        ("admin.sidebar_unlock", "admin", "جمع‌کردن سایدبار (فقط آیکون)", "Collapse sidebar (icons only)", "طي الشريط (أيقونات فقط)"),
        ("admin.nav.my_apikeys", "admin", "کلیدهای API من", "My API keys", "مفاتيح API الخاصة بي"),
        ("admin.nav.apikeys", "admin", "مدیریت API Keys", "API Keys (admin)", "إدارة مفاتيح API"),
        ("admin.nav.themes", "admin", "تم‌ها", "Themes", "السمات"),
    };
}
