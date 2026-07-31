namespace BlogApp.Services;

public static partial class UiTranslationCatalog
{
    /// <summary>Accessibility + missing admin nav labels.</summary>
    public static readonly (string Key, string Group, string Fa, string En, string Ar)[] Accessibility =
    {
        ("admin.nav.accessibility", "admin", "دسترسی‌پذیری", "Accessibility", "إمكانية الوصول"),
        ("admin.nav.apikeys", "admin", "کلیدهای API", "API keys", "مفاتيح API"),
        ("admin.nav.my_apikeys", "admin", "کلیدهای API من", "My API keys", "مفاتيح API الخاصة بي"),
        ("admin.nav.monetization", "admin", "درآمدزایی", "Monetization", "تحقيق الدخل"),
        ("a11y.panel_title", "a11y", "دسترسی‌پذیری", "Accessibility", "إمكانية الوصول"),
        ("a11y.high_contrast", "a11y", "کنتراست بالا", "High contrast", "تباين عالٍ"),
        ("a11y.underline_links", "a11y", "زیرخط لینک‌ها", "Underline links", "تسطير الروابط"),
        ("a11y.reduce_motion", "a11y", "کاهش حرکت", "Reduce motion", "تقليل الحركة"),
        ("a11y.text_size", "a11y", "اندازه متن", "Text size", "حجم النص"),
    };
}
