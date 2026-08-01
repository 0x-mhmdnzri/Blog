namespace AVICRM.Services;

public static partial class UiTranslationCatalog
{
    public static readonly (string Key, string Group, string Fa, string En, string Ar)[] Analytics =
    {
        ("admin.nav.analytics", "admin", "تحلیل‌ها", "Analytics", "التحليلات"),

        ("ana.title", "ana", "تحلیل‌ها", "Analytics", "التحليلات"),
        ("ana.subtitle", "ana",
            "رفتار بازدیدکنندگان: منبع ترافیک، دستگاه، جغرافیا، جست‌وجو و نقشه حرارتی — جدا از داشبورد عملیاتی.",
            "Visitor behavior: traffic sources, devices, geo, search & heatmaps — separate from the ops dashboard.",
            "سلوك الزوار: مصادر الزيارات والأجهزة والجغرافيا والبحث والخرائط الحرارية."),
        ("ana.back_ops", "ana", "داشبورد عملیاتی", "Ops dashboard", "لوحة التشغيل"),
        ("ana.back_analytics", "ana", "بازگشت به تحلیل‌ها", "Back to analytics", "العودة إلى التحليلات"),
        ("ana.back_heatmap_list", "ana", "لیست نوشته‌ها", "Back to post list", "قائمة المقالات"),

        ("ana.kpi_views", "ana", "بازدید بازه", "Range views", "مشاهدات الفترة"),
        ("ana.in_range", "ana", "در بازه انتخاب‌شده", "In selected range", "في الفترة المحددة"),
        ("ana.kpi_unique", "ana", "بازدیدکننده یکتا", "Unique visitors", "زوار فريدون"),
        ("ana.kpi_vpv", "ana", "بازدید / نفر", "Views / visitor", "مشاهدة / زائر"),
        ("ana.kpi_bounce", "ana", "نرخ پرش", "Bounce rate", "معدل الارتداد"),
        ("ana.sessions", "ana", "نشست", "sessions", "جلسات"),
        ("ana.kpi_read", "ana", "میانگین مطالعه", "Avg. reading time", "متوسط القراءة"),
        ("ana.kpi_returning", "ana", "بازگشتی", "Returning", "عائدون"),
        ("ana.kpi_searches", "ana", "جست‌وجوها", "Searches", "عمليات البحث"),
        ("ana.kpi_heatmap_clicks", "ana", "کلیک نقشه حرارتی", "Heatmap clicks", "نقرات الخريطة"),
        ("ana.kpi_sources", "ana", "منابع فعال", "Active sources", "مصادر نشطة"),
        ("ana.kpi_countries", "ana", "کشورها", "Countries", "الدول"),

        ("ana.chart_views", "ana", "روند بازدید", "Views over time", "اتجاه المشاهدات"),
        ("ana.chart_views_sub", "ana", "ترافیک واقعی خوانندگان در بازه", "Real reader traffic in range", "حركة القراء الحقيقية"),
        ("ana.chart_sources", "ana", "منابع ترافیک", "Traffic sources", "مصادر الزيارات"),
        ("ana.chart_sources_sub", "ana", "direct / search / social / referral / utm", "direct / search / social / referral / utm", "مباشر / بحث / اجتماعي / إحالة"),
        ("ana.chart_hours", "ana", "بازدید بر اساس ساعت (UTC)", "Views by hour (UTC)", "المشاهدات حسب الساعة (UTC)"),
        ("ana.chart_hours_sub", "ana", "زمان‌های اوج مطالعه در شبانه‌روز", "Peak reading hours across the day", "ساعات الذروة خلال اليوم"),
        ("ana.chart_device", "ana", "دستگاه", "Device", "الجهاز"),
        ("ana.chart_browser", "ana", "مرورگر", "Browser", "المتصفح"),
        ("ana.chart_os", "ana", "سیستم‌عامل", "OS", "نظام التشغيل"),
        ("ana.chart_geo", "ana", "جغرافیا", "Geography", "الجغرافيا"),

        ("ana.popular", "ana", "پربازدیدترین (همه زمان‌ها)", "Most popular (all-time)", "الأكثر مشاهدة"),
        ("ana.trending", "ana", "پرطرفدار (۳ روز اخیر)", "Trending (last 3 days)", "الرائج (آخر 3 أيام)"),
        ("ana.referrers", "ana", "ارجاع‌دهنده‌ها", "Referrers", "المُحيلون"),
        ("ana.search_kw", "ana", "کلمات جست‌وجو", "Search keywords", "كلمات البحث"),
        ("ana.heatmap", "ana", "نقشه حرارتی کلیک", "Click heatmap", "خريطة النقرات"),
        ("ana.heatmap_sub", "ana", "نقاط نسبی روی بدنه نوشته", "Relative points on post body", "نقاط نسبية على جسم المقال"),
        ("ana.heatmap_cta_sub", "ana", "برای هر نوشته نقشه جداگانه ببینید", "Open a post to view its heatmap", "افتح مقالاً لعرض خريطته"),
        ("ana.heatmap_list_title", "ana", "نقشه‌های حرارتی نوشته‌ها", "Post heatmaps", "خرائط حرارية للمقالات"),
        ("ana.heatmap_list_sub", "ana", "یک نوشته را انتخاب کنید تا نقشه کلیک آن را ببینید.", "Pick a post to open its click heatmap.", "اختر مقالاً لفتح خريطة النقرات."),
        ("ana.heatmap_open_list", "ana", "مشاهده نقشه‌های حرارتی", "View heatmaps", "عرض الخرائط الحرارية"),
        ("ana.heatmap_open", "ana", "نقشه حرارتی", "Heatmap", "الخريطة"),
        ("ana.heatmap_cells", "ana", "ناحیه فعال", "hot zones", "مناطق نشطة"),
        ("ana.heatmap_hotspots", "ana", "داغ‌ترین نقاط", "Hottest cells", "أكثر الخلايا سخونة"),
        ("ana.btn_open_post", "ana", "مشاهده نوشته", "Open post", "فتح المقال"),
        ("ana.range_all", "ana", "همه زمان‌ها", "All time", "كل الأوقات"),
        ("ana.col_clicks", "ana", "کلیک‌ها", "Clicks", "النقرات"),

        ("ana.col_all_time", "ana", "کل", "All-time", "الإجمالي"),
        ("ana.col_range", "ana", "بازه", "Range", "الفترة"),
        ("ana.col_recent", "ana", "اخیر", "Recent", "الأخير"),
        ("ana.col_host", "ana", "میزبان", "Host", "المضيف"),
        ("ana.col_count", "ana", "تعداد", "Count", "العدد"),
        ("ana.col_query", "ana", "عبارت", "Query", "الاستعلام"),
        ("ana.clicks", "ana", "کلیک", "clicks", "نقرات"),

        ("ana.empty_posts", "ana", "هنوز نوشته‌ای برای رتبه‌بندی نیست.", "No posts to rank yet.", "لا مقالات للترتيب بعد."),
        ("ana.empty_trending", "ana", "در ۳ روز اخیر روندی ثبت نشده.", "No trending activity in the last 3 days.", "لا نشاط رائج في آخر 3 أيام."),
        ("ana.empty_referrers", "ana", "ارجاع خارجی ثبت نشده.", "No external referrers recorded.", "لا محيلين خارجيين."),
        ("ana.empty_search", "ana", "هنوز جست‌وجویی ثبت نشده.", "No searches recorded yet.", "لا عمليات بحث بعد."),
        ("ana.empty_heatmap", "ana", "هنوز کلیکی ثبت نشده — روی بدنه نوشته‌ها کلیک کنید.", "No clicks yet — interact with post bodies.", "لا نقرات بعد — تفاعل مع نصوص المقالات."),
        ("ana.no_posts", "ana", "نوشته‌ای نیست", "No posts", "لا مقالات"),
    };
}
