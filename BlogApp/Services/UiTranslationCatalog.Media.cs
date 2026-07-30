namespace BlogApp.Services;

public static partial class UiTranslationCatalog
{
    public static readonly (string Key, string Group, string Fa, string En, string Ar)[] Media =
    {
        ("media.kpi_total", "media", "کل رسانه", "Total media", "إجمالي الوسائط"),
        ("media.kpi_images", "media", "تصاویر", "Images", "صور"),
        ("media.kpi_videos", "media", "ویدیوها", "Videos", "فيديوهات"),
        ("media.kpi_size", "media", "حجم کل", "Total size", "الحجم الكلي"),

        ("media.upload", "media", "آپلود رسانه", "Upload media", "رفع وسائط"),
        ("media.file", "media", "فایل", "File", "ملف"),
        ("media.upload_hint", "media", "jpg/png/gif/webp تا ۸MB · mp4/webm تا ۲۰۰MB", "jpg/png/gif/webp up to 8MB · mp4/webm up to 200MB", "jpg/png/gif/webp حتى 8MB · mp4/webm حتى 200MB"),
        ("media.upload_btn", "media", "آپلود", "Upload", "رفع"),

        ("media.all", "media", "همه", "All", "الكل"),
        ("media.images", "media", "تصویر", "Images", "صور"),
        ("media.videos", "media", "ویدیو", "Videos", "فيديو"),
        ("media.search", "media", "جست‌وجوی نام یا نوع…", "Search name or type…", "بحث بالاسم أو النوع…"),

        ("media.grid_hint", "media", "پیش‌نمایش · کپی URL/Markdown · حذف تکی یا گروهی", "Preview · copy URL/Markdown · single or bulk delete", "معاينة · نسخ الرابط · حذف فردي أو جماعي"),
        ("media.bulk_delete", "media", "حذف انتخاب‌شده‌ها", "Delete selected", "حذف المحدد"),
        ("media.confirm_bulk", "media", "فایل‌های انتخاب‌شده حذف شوند؟", "Delete selected files?", "حذف الملفات المحددة؟"),
        ("media.empty", "media", "رسانه‌ای یافت نشد. اولین فایل را آپلود کنید.", "No media yet. Upload your first file.", "لا وسائط بعد. ارفع أول ملف."),

        ("media.usage", "media", "استفاده", "Usage", "استخدام"),
        ("media.usage_title", "media", "استفاده در نوشته‌ها", "Used in posts", "مستخدم في المقالات"),
        ("media.usage_none", "media", "در هیچ نوشته‌ای پیدا نشد (ممکن است فقط در پیش‌نویس یا حذف‌شده باشد).", "Not found in any post (may be draft-only or unused).", "غير موجود في أي مقال."),

        ("media.uploaded", "media", "آپلود شد — شناسه #{0}", "Uploaded — id #{0}", "تم الرفع — #{0}"),
        ("media.deleted", "media", "رسانه حذف شد.", "Media deleted.", "تم حذف الوسائط."),
        ("media.bulk_deleted", "media", "{0} مورد حذف شد.", "{0} item(s) deleted.", "تم حذف {0}."),
        ("media.err_post", "media", "نوشته معتبر نیست یا دسترسی ندارید.", "Invalid post or access denied.", "مقال غير صالح أو لا صلاحية."),
        ("media.err_exec", "media", "نوع فایل اجرایی مجاز نیست.", "Executable files are not allowed.", "الملفات التنفيذية غير مسموحة."),
    };
}
