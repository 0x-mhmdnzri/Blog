namespace BlogApp.Services;

public static partial class UiTranslationCatalog
{
    public static readonly (string Key, string Group, string Fa, string En, string Ar)[] Moderation =
    {
        ("mod.eyebrow", "mod", "بررسی محتوا", "Moderation", "المراجعة"),
        ("mod.subtitle", "mod", "صف واحد برای دیدگاه‌های در انتظار، گزارش‌ها و اسپم.", "Unified queue for pending comments, reports, and spam.", "قائمة موحّدة للتعليقات المعلقة والبلاغات والرسائل المزعجة."),
        ("mod.filter_all", "mod", "همه", "All", "الكل"),
        ("mod.spam", "mod", "اسپم", "Spam", "مزعج"),
        ("mod.search_placeholder", "mod", "جست‌وجو در صف…", "Search queue…", "بحث في القائمة…"),
        ("mod.items_suffix", "mod", "مورد", "items", "عناصر"),
        ("mod.queue_empty_title", "mod", "صف خالی است", "Queue is empty", "القائمة فارغة"),
        ("mod.badge_comment", "mod", "دیدگاه", "Comment", "تعليق"),
        ("mod.badge_report", "mod", "گزارش", "Report", "بلاغ"),
        ("mod.reporter", "mod", "گزارش‌دهنده", "Reporter", "المبلّغ"),
        ("mod.none_for_filter", "mod", "موردی با این فیلتر نیست.", "No items match this filter.", "لا توجد عناصر لهذا التصفية."),
    };
}
