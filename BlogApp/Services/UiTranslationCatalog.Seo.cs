namespace BlogApp.Services;

public static partial class UiTranslationCatalog
{
    public static readonly (string Key, string Group, string Fa, string En, string Ar)[] Seo =
    {
        ("seo.tab_overview", "seo", "نمای کلی", "Overview", "نظرة عامة"),
        ("seo.tab_meta", "seo", "متادیتا و robots", "Meta & robots", "البيانات و robots"),
        ("seo.tab_redirects", "seo", "ریدایرکت‌ها", "Redirects", "إعادة التوجيه"),
        ("seo.tab_broken", "seo", "لینک‌های شکسته", "Broken links", "روابط معطلة"),
        ("seo.tab_health", "seo", "سلامت نوشته‌ها", "Post health", "صحة المقالات"),

        ("seo.kpi_published", "seo", "منتشرشده", "Published", "منشور"),
        ("seo.kpi_redirects", "seo", "ریدایرکت", "Redirects", "تحويلات"),
        ("seo.kpi_broken", "seo", "لینک شکسته", "Broken links", "روابط معطلة"),
        ("seo.kpi_no_summary", "seo", "بدون خلاصه", "No summary", "بدون ملخص"),

        ("seo.live_endpoints", "seo", "نقاط زنده", "Live endpoints", "نقاط حية"),
        ("seo.live_hint", "seo", "sitemap و robots همیشه از داده‌های زنده ساخته می‌شوند.", "Sitemap and robots are generated from live data.", "يتم إنشاء sitemap و robots من بيانات حية."),
        ("seo.capabilities", "seo", "قابلیت‌های فعال", "Active capabilities", "القدرات النشطة"),
        ("seo.cap_meta", "seo", "مدیریت نام سایت، توضیح، توییتر و Base URL", "Site name, description, Twitter handle, Base URL", "اسم الموقع والوصف وتويتر والرابط الأساسي"),
        ("seo.cap_redirect", "seo", "ریدایرکت ۳۰۱/۳۰۲ با شمارش بازدید", "301/302 redirects with hit counts", "إعادة توجيه 301/302 مع العداد"),
        ("seo.cap_broken", "seo", "اسکن لینک‌های داخلی شکسته در مارک‌داون", "Broken internal link scan in Markdown", "فحص الروابط الداخلية المعطلة"),
        ("seo.cap_sitemap", "seo", "sitemap.xml پویا برای نوشته‌ها و دسته‌ها", "Dynamic sitemap for posts and categories", "خريطة موقع ديناميكية"),
        ("seo.cap_og", "seo", "Open Graph / Twitter Cards و JSON-LD در صفحات عمومی", "Open Graph / Twitter Cards and JSON-LD on public pages", "Open Graph و JSON-LD في الصفحات العامة"),

        ("seo.meta_super_only", "seo", "ویرایش متادیتای سایت فقط برای سوپرادمین است.", "Site meta can only be edited by SuperAdmin.", "تعديل بيانات الموقع لـ SuperAdmin فقط."),
        ("seo.site_name", "seo", "نام سایت", "Site name", "اسم الموقع"),
        ("seo.site_desc", "seo", "توضیح سایت", "Site description", "وصف الموقع"),
        ("seo.author_name", "seo", "نام نویسنده پیش‌فرض", "Default author name", "اسم المؤلف الافتراضي"),
        ("seo.twitter", "seo", "هندل توییتر", "Twitter handle", "حساب تويتر"),
        ("seo.base_url", "seo", "آدرس پایه (canonical)", "Base URL (canonical)", "الرابط الأساسي"),
        ("seo.robots_custom", "seo", "robots.txt سفارشی (اختیاری)", "Custom robots.txt (optional)", "robots.txt مخصص (اختياري)"),
        ("seo.robots_hint", "seo", "خالی بگذارید تا نسخه پیش‌فرض تولید شود.", "Leave empty to use the generated default.", "اتركه فارغًا لاستخدام الافتراضي."),
        ("seo.save_meta", "seo", "ذخیره متادیتا", "Save meta", "حفظ البيانات"),

        ("seo.add_redirect", "seo", "افزودن ریدایرکت", "Add redirect", "إضافة تحويل"),
        ("seo.from_path", "seo", "مسیر مبدأ", "From path", "من المسار"),
        ("seo.to_url", "seo", "مقصد", "To URL", "إلى الرابط"),
        ("seo.status", "seo", "کد", "Status", "الحالة"),
        ("seo.notes", "seo", "یادداشت", "Notes", "ملاحظات"),
        ("seo.add", "seo", "افزودن", "Add", "إضافة"),
        ("seo.hits", "seo", "بازدید", "Hits", "زيارات"),
        ("seo.enable", "seo", "فعال", "Enable", "تفعيل"),
        ("seo.disable", "seo", "غیرفعال", "Disable", "تعطيل"),
        ("seo.empty_redirects", "seo", "هنوز ریدایرکتی ثبت نشده است.", "No redirects yet.", "لا توجد تحويلات بعد."),

        ("seo.broken_hint", "seo", "لینک‌های داخلی داخل مارک‌داون نوشته‌های منتشرشده را اسکن می‌کند.", "Scans internal links inside published post Markdown.", "يفحص الروابط الداخلية في مقالات منشورة."),
        ("seo.scan_now", "seo", "اسکن اکنون", "Scan now", "فحص الآن"),
        ("seo.empty_broken", "seo", "لینک شکسته‌ای پیدا نشد (یا هنوز اسکن نکرده‌اید).", "No broken links found (or not scanned yet).", "لا روابط معطلة (أو لم يتم الفحص)."),
        ("seo.broken_url", "seo", "آدرس", "URL", "الرابط"),
        ("seo.detected", "seo", "کشف", "Detected", "اكتشاف"),

        ("seo.health_hint", "seo", "امتیاز بر اساس وجود خلاصه و تصویر کاور است (برای OG/SEO).", "Score is based on summary and cover image (for OG/SEO).", "النقاط تعتمد على الملخص وصورة الغلاف."),
        ("seo.empty_health", "seo", "نوشته منتشرشده‌ای نیست.", "No published posts.", "لا مقالات منشورة."),
        ("seo.has_summary", "seo", "خلاصه", "Summary", "ملخص"),
        ("seo.has_cover", "seo", "کاور", "Cover", "غلاف"),
        ("seo.score", "seo", "امتیاز", "Score", "نقاط"),

        ("seo.saved_meta", "seo", "متادیتا ذخیره شد.", "Meta saved.", "تم حفظ البيانات."),
        ("seo.saved_redirect", "seo", "ریدایرکت ذخیره شد.", "Redirect saved.", "تم حفظ التحويل."),
        ("seo.deleted_redirect", "seo", "ریدایرکت حذف شد.", "Redirect deleted.", "تم حذف التحويل."),
        ("seo.scan_done", "seo", "اسکن تمام شد — {0} مورد پیدا شد.", "Scan finished — {0} issue(s) found.", "انتهى الفحص — {0} مشكلة."),
        ("seo.err_redirect_form", "seo", "فرم ریدایرکت نامعتبر است.", "Invalid redirect form.", "نموذج التحويل غير صالح."),
        ("seo.err_from_path", "seo", "مسیر مبدأ نامعتبر است.", "Invalid from path.", "مسار المصدر غير صالح."),
        ("seo.err_to_url", "seo", "مقصد نامعتبر است.", "Invalid destination URL.", "الوجهة غير صالحة."),
    };
}
