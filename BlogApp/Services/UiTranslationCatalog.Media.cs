namespace BlogApp.Services;

public static partial class UiTranslationCatalog
{
    public static readonly (string Key, string Group, string Fa, string En, string Ar)[] Media =
    {
        ("media.subtitle", "media", "کتابخانه رسانه — آپلود، جست‌وجو، کپی لینک و مدیریت فایل‌ها", "Media library — upload, search, copy links, manage files", "مكتبة الوسائط — رفع وبحث ونسخ الروابط"),
        ("media.stats_aria", "media", "آمار رسانه", "Media stats", "إحصاءات الوسائط"),
        ("media.view_aria", "media", "نمای شبکه / لیست", "Grid / list view", "عرض شبكة / قائمة"),
        ("media.view_grid", "media", "شبکه", "Grid", "شبكة"),
        ("media.view_list", "media", "لیست", "List", "قائمة"),

        ("media.kpi_total", "media", "کل رسانه", "Total media", "إجمالي الوسائط"),
        ("media.kpi_images", "media", "تصاویر", "Images", "صور"),
        ("media.kpi_videos", "media", "ویدیوها", "Videos", "فيديوهات"),
        ("media.kpi_size", "media", "حجم کل", "Total size", "الحجم الكلي"),

        ("media.upload", "media", "آپلود رسانه", "Upload media", "رفع وسائط"),
        ("media.file", "media", "فایل", "File", "ملف"),
        ("media.upload_hint", "media", "jpg/png/gif/webp تا ۸MB · mp4/webm تا ۲۰۰MB", "jpg/png/gif/webp up to 8MB · mp4/webm up to 200MB", "jpg/png/gif/webp حتى 8MB · mp4/webm حتى 200MB"),
        ("media.upload_btn", "media", "آپلود", "Upload", "رفع"),
        ("media.drop_title", "media", "فایل را اینجا رها کنید یا انتخاب کنید", "Drop a file here or choose one", "أسقط ملفًا هنا أو اختر"),
        ("media.drop_active", "media", "رها کنید…", "Drop now…", "أفلت الآن…"),
        ("media.pick_file", "media", "انتخاب فایل", "Choose file", "اختر ملفًا"),

        ("media.all", "media", "همه", "All", "الكل"),
        ("media.images", "media", "تصویر", "Images", "صور"),
        ("media.videos", "media", "ویدیو", "Videos", "فيديو"),
        ("media.search", "media", "جست‌وجوی نام…", "Search by name…", "بحث بالاسم…"),

        ("media.grid_hint", "media", "پیش‌نمایش · کپی URL/Markdown · حذف تکی یا گروهی", "Preview · copy URL/Markdown · single or bulk delete", "معاينة · نسخ · حذف"),
        ("media.bulk_delete", "media", "حذف انتخاب‌شده‌ها", "Delete selected", "حذف المحدد"),
        ("media.confirm_bulk", "media", "فایل‌های انتخاب‌شده حذف شوند؟", "Delete selected files?", "حذف الملفات المحددة؟"),
        ("media.empty", "media", "هنوز رسانه‌ای نیست", "No media yet", "لا وسائط بعد"),
        ("media.empty_hint", "media", "اولین تصویر یا ویدیو را آپلود کنید تا اینجا دیده شود.", "Upload your first image or video to see it here.", "ارفع أول صورة أو فيديو."),

        ("media.select_all", "media", "انتخاب همه", "Select all", "تحديد الكل"),
        ("media.clear_sel", "media", "پاک کردن انتخاب", "Clear selection", "مسح التحديد"),
        ("media.selected_zero", "media", "هیچ موردی انتخاب نشده", "Nothing selected", "لا شيء محدد"),
        ("media.selected_n", "media", "{0} مورد انتخاب شده", "{0} selected", "{0} محدد"),
        ("media.preview", "media", "پیش‌نمایش", "Preview", "معاينة"),
        ("media.copy_url", "media", "کپی آدرس", "Copy URL", "نسخ الرابط"),
        ("media.copy_md", "media", "کپی مارک‌داون", "Copy Markdown", "نسخ Markdown"),
        ("media.copied", "media", "کپی شد", "Copied", "تم النسخ"),

        ("media.usage", "media", "استفاده", "Usage", "استخدام"),
        ("media.usage_title", "media", "استفاده در نوشته‌ها", "Used in posts", "مستخدم في المقالات"),
        ("media.usage_none", "media", "در هیچ نوشته‌ای پیدا نشد (ممکن است فقط در پیش‌نویس یا بدون استفاده باشد).", "Not found in any post (may be unused or draft-only).", "غير موجود في أي مقال."),

        ("media.uploaded", "media", "آپلود شد — شناسه #{0}", "Uploaded — id #{0}", "تم الرفع — #{0}"),
        ("media.deleted", "media", "رسانه حذف شد.", "Media deleted.", "تم حذف الوسائط."),
        ("media.bulk_deleted", "media", "{0} مورد حذف شد.", "{0} item(s) deleted.", "تم حذف {0}."),
        ("media.err_post", "media", "نوشته معتبر نیست یا دسترسی ندارید.", "Invalid post or access denied.", "مقال غير صالح أو لا صلاحية."),
        ("media.err_exec", "media", "نوع فایل اجرایی مجاز نیست.", "Executable files are not allowed.", "الملفات التنفيذية غير مسموحة."),

        ("media.reoptimize", "media", "بهینه‌سازی مجدد", "Re-optimize", "إعادة التحسين"),
        ("media.reoptimize_queued", "media", "بهینه‌سازی تصویر #{0} در صف قرار گرفت.", "Image #{0} queued for optimize.", "تمت جدولة تحسين الصورة #{0}."),
        ("media.err_not_image", "media", "فقط تصاویر قابل بهینه‌سازی هستند.", "Only images can be optimized.", "يمكن تحسين الصور فقط."),
        ("media.versions", "media", "نسخه‌ها", "Versions", "الإصدارات"),
        ("media.version_restored", "media", "نسخه #{0} بازیابی شد.", "Version #{0} restored.", "تم استعادة الإصدار #{0}."),
        ("media.variants", "media", "عرض‌های واکنش‌گرا", "Responsive widths", "عروض متجاوبة"),
    };
}
