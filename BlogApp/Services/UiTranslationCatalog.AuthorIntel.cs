namespace BlogApp.Services;

public static partial class UiTranslationCatalog
{
    public static readonly (string Key, string Group, string Fa, string En, string Ar)[] AuthorIntel =
    {
        ("admin.nav.author_intel", "admin", "هوش نویسندگان", "Author Intelligence", "ذكاء المؤلفين"),

        ("ai.title", "ai", "هوش نویسندگان", "Author Intelligence", "ذكاء المؤلفين"),
        ("ai.subtitle", "ai", "تحلیل رفتار، سلامت تعامل و پروفایل سازندگان — فقط سوپر ادمین", "Behavioral analytics, engagement health, and creator profiles — SuperAdmin only", "تحليلات السلوك وصحة التفاعل وملفات المبدعين — للمدير الأعلى فقط"),
        ("ai.search_ph", "ai", "جستجوی نویسنده…", "Search authors…", "ابحث عن مؤلفين…"),
        ("ai.search_btn", "ai", "جستجو", "Search", "بحث"),

        ("ai.sort_health", "ai", "سلامت", "Health", "الصحة"),
        ("ai.sort_views", "ai", "بازدید", "Views", "المشاهدات"),
        ("ai.sort_posts", "ai", "نوشته‌ها", "Posts", "المقالات"),
        ("ai.sort_followers", "ai", "دنبال‌کننده", "Followers", "المتابعون"),
        ("ai.sort_engagement", "ai", "تعامل", "Engagement", "التفاعل"),
        ("ai.sort_recent", "ai", "اخیر", "Recent", "الأحدث"),
        ("ai.sort_name", "ai", "نام", "Name", "الاسم"),

        ("ai.kpi_authors", "ai", "نویسندگان", "Authors", "المؤلفون"),
        ("ai.kpi_active", "ai", "فعال", "Active", "نشط"),
        ("ai.kpi_rising", "ai", "رو به رشد", "Rising", "صاعد"),
        ("ai.kpi_dormant", "ai", "راکد", "Dormant", "خامل"),

        ("ai.stat_posts", "ai", "نوشته", "posts", "مقالات"),
        ("ai.stat_views_30d", "ai", "بازدید ۳۰روز", "views 30d", "مشاهدات ٣٠ي"),
        ("ai.stat_likes", "ai", "پسند", "likes", "إعجابات"),
        ("ai.stat_followers", "ai", "دنبال‌کننده", "followers", "متابعون"),
        ("ai.stat_eng", "ai", "تعامل", "eng.", "تفاعل"),
        ("ai.stat_streak", "ai", "رشته", "streak", "سلسلة"),

        ("ai.last_publish", "ai", "آخرین انتشار", "Last publish", "آخر نشر"),
        ("ai.no_publishes", "ai", "هنوز منتشر نشده", "No publishes yet", "لا نشر بعد"),
        ("ai.reports_badge", "ai", "گزارش", "report(s)", "بلاغ"),
        ("ai.empty", "ai", "نویسنده‌ای با این فیلتر یافت نشد.", "No authors match this filter.", "لا مؤلفون يطابقون هذا التصفية."),
        ("ai.empty_hint", "ai", "عبارت جستجو را تغییر دهید یا فیلتر را پاک کنید.", "Try a different search or clear the filter.", "جرّب بحثاً مختلفاً أو امسح التصفية."),

        ("ai.health_excellent", "ai", "عالی", "Excellent", "ممتاز"),
        ("ai.health_good", "ai", "خوب", "Good", "جيد"),
        ("ai.health_neutral", "ai", "متوسط", "Neutral", "محايد"),
        ("ai.health_weak", "ai", "ضعیف", "Weak", "ضعيف"),
        ("ai.health_critical", "ai", "بحرانی", "Critical", "حرج"),

        ("ai.momentum_rising", "ai", "رو به رشد", "rising", "صاعد"),
        ("ai.momentum_stable", "ai", "پایدار", "stable", "مستقر"),
        ("ai.momentum_declining", "ai", "نزولی", "declining", "متراجع"),
        ("ai.momentum_dormant", "ai", "راکد", "dormant", "خامل"),

        ("ai.back", "ai", "همه نویسندگان", "All authors", "كل المؤلفين"),
        ("ai.range_30", "ai", "۳۰ روز", "30d", "٣٠ يوماً"),
        ("ai.range_90", "ai", "۹۰ روز", "90d", "٩٠ يوماً"),
        ("ai.range_180", "ai", "۱۸۰ روز", "180d", "١٨٠ يوماً"),

        ("ai.section_insights", "ai", "بینش‌ها", "Insights", "رؤى"),
        ("ai.section_overview", "ai", "نمای کلی", "Overview", "نظرة عامة"),
        ("ai.section_recent", "ai", "نوشته‌های اخیر", "Recent posts", "أحدث المقالات"),
        ("ai.section_top_views", "ai", "پربازدیدترین", "Top by views", "الأكثر مشاهدة"),
        ("ai.section_top_eng", "ai", "بیشترین تعامل", "Top by engagement", "الأكثر تفاعلاً"),
        ("ai.section_publishing", "ai", "انتشار", "Publishing", "النشر"),
        ("ai.section_engagement", "ai", "تعامل", "Engagement", "التفاعل"),
        ("ai.section_audience", "ai", "مخاطب", "Audience", "الجمهور"),
        ("ai.section_risk", "ai", "ریسک و نظارت", "Risk & moderation", "المخاطر والإشراف"),

        ("ai.kpi_published", "ai", "منتشرشده", "Published", "منشور"),
        ("ai.kpi_total_views", "ai", "بازدید کل", "Total views", "إجمالي المشاهدات"),
        ("ai.kpi_likes", "ai", "پسندها", "Likes", "الإعجابات"),
        ("ai.kpi_followers", "ai", "دنبال‌کنندگان", "Followers", "المتابعون"),
        ("ai.kpi_eng_rate", "ai", "نرخ تعامل", "Eng. rate", "معدل التفاعل"),
        ("ai.kpi_streak", "ai", "رشته فعلی", "Streak", "السلسلة"),
        ("ai.kpi_best_streak", "ai", "بهترین رشته", "Best streak", "أفضل سلسلة"),
        ("ai.kpi_open_reports", "ai", "گزارش باز", "Open reports", "بلاغات مفتوحة"),
        ("ai.kpi_drafts", "ai", "پیش‌نویس", "Drafts", "مسودات"),
        ("ai.kpi_unique", "ai", "بازدیدکننده یکتا", "Unique 30d", "زوار فريدون"),
        ("ai.kpi_returning", "ai", "بازگشتی", "Returning", "عائدون"),
        ("ai.kpi_avg_views", "ai", "میانگین بازدید", "Avg views/post", "متوسط المشاهدات"),
        ("ai.kpi_avg_read", "ai", "میانگین مطالعه", "Avg read", "متوسط القراءة"),
        ("ai.kpi_bookmarks", "ai", "نشان‌ها", "Bookmarks", "الإشارات"),
        ("ai.kpi_pending", "ai", "در انتظار بررسی", "Pending review", "قيد المراجعة"),
        ("ai.kpi_rejected", "ai", "ردشده", "Rejected", "مرفوض"),
        ("ai.kpi_comments", "ai", "دیدگاه‌ها", "Comments", "التعليقات"),
        ("ai.kpi_avg_words", "ai", "میانگین کلمات", "Avg words", "متوسط الكلمات"),

        ("ai.col_title", "ai", "عنوان", "Title", "العنوان"),
        ("ai.col_views", "ai", "بازدید", "Views", "المشاهدات"),
        ("ai.col_likes", "ai", "پسند", "Likes", "إعجابات"),
        ("ai.col_comments", "ai", "دیدگاه", "Comments", "تعليقات"),
        ("ai.col_published", "ai", "انتشار", "Published", "النشر"),
        ("ai.col_eng", "ai", "تعامل", "Engagement", "التفاعل"),

        ("ai.joined", "ai", "عضویت", "Joined", "انضم"),
        ("ai.no_posts", "ai", "نوشته منتشرشده‌ای نیست.", "No published posts.", "لا مقالات منشورة."),
        ("ai.detail_hint", "ai", "نمای کامل تحلیل به‌زودی گسترش می‌یابد — کارت‌های فهرست فعال‌اند.", "Full detail charts expanding next — Index cards are live.", "مخططات التفاصيل قادمة — بطاقات القائمة جاهزة."),
        ("ai.view_profile", "ai", "پروفایل عمومی", "Public profile", "الملف العام"),
    };
}
