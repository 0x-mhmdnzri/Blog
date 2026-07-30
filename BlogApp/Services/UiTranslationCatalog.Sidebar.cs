namespace BlogApp.Services;

public static partial class UiTranslationCatalog
{
    public static readonly (string Key, string Group, string Fa, string En, string Ar)[] Sidebar =
    {
        ("admin.sidebar_lock", "admin", "قفل سایدبار (نمایش کامل)", "Lock sidebar (full labels)", "تثبيت الشريط (تسميات كاملة)"),
        ("admin.sidebar_unlock", "admin", "جمع‌کردن سایدبار (فقط آیکون)", "Collapse sidebar (icons only)", "طي الشريط (أيقونات فقط)"),
    };
}
