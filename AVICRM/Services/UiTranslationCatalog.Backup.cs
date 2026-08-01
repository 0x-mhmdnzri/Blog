namespace AVICRM.Services;

/// <summary>SuperAdmin Backup &amp; storage page — FA / EN / AR.</summary>
public static partial class UiTranslationCatalog
{
    public static readonly (string Key, string Group, string Fa, string En, string Ar)[] Backup =
    {
        ("admin.nav.backup", "admin", "پشتیبان و ذخیره‌سازی", "Backup & storage", "النسخ الاحتياطي والتخزين"),

        ("bk.title", "bk", "پشتیبان کامل برنامه", "Full application backup", "نسخة احتياطية كاملة للتطبيق"),
        ("bk.eyebrow", "bk", "مدیر ارشد · بازیابی از حادثه", "SuperAdmin · Disaster recovery", "المسؤول الأعلى · التعافي من الكوارث"),
        ("bk.lead", "bk",
            "اسنپ‌شات داغ SQLite به‌همراه درخت داده به‌صورت ZIP روی فضای داده ذخیره می‌شود. برای نگه‌داری خارج از سرور دانلود کنید. نشانگرهای زنده ظرفیت دیسک و ورودی/خروجی فرایند را نمایش می‌دهند.",
            "Hot SQLite snapshot + data tree compressed to a portable zip on the data volume. Download for off-box retention. Live gauges track volume capacity and process I/O.",
            "لقطة SQLite حية مع شجرة البيانات كملف ZIP على مجلد البيانات. حمّلها للاحتفاظ خارج الخادم. المقاييس المباشرة تعرض سعة الحجم وإدخال/إخراج العملية."),

        ("bk.create_download", "bk", "ایجاد و دانلود ZIP", "Create & download ZIP", "إنشاء وتنزيل ZIP"),
        ("bk.create_only", "bk", "فقط ایجاد (روی دیسک بماند)", "Create only (keep on volume)", "إنشاء فقط (الإبقاء على الحجم)"),
        ("bk.creating", "bk", "در حال ساخت اسنپ‌شات…", "Creating snapshot…", "جارٍ إنشاء اللقطة…"),

        ("bk.volume", "bk", "حجم دیسک", "Volume", "الحجم"),
        ("bk.live", "bk", "زنده", "Live", "مباشر"),
        ("bk.used", "bk", "مصرف‌شده", "used", "مستخدم"),
        ("bk.total", "bk", "کل", "Total", "الإجمالي"),
        ("bk.free", "bk", "آزاد", "Free", "متاح"),
        ("bk.mount", "bk", "نقطهٔ اتصال", "Mount", "نقطة التركيب"),

        ("bk.app_data", "bk", "دادهٔ برنامه", "Application data", "بيانات التطبيق"),
        ("bk.database", "bk", "پایگاه‌داده", "Database", "قاعدة البيانات"),
        ("bk.wal", "bk", "WAL / SHM", "WAL / SHM", "WAL / SHM"),
        ("bk.media", "bk", "رسانه / آپلود", "Media / uploads", "الوسائط / الرفع"),
        ("bk.data_root", "bk", "ریشهٔ داده (بدون بکاپ)", "Data root (ex. backups)", "جذر البيانات (بدون النسخ)"),
        ("bk.backups_on_volume", "bk", "بکاپ‌های روی دیسک", "Backups on volume", "النسخ على الحجم"),

        ("bk.process_io", "bk", "I/O فرایند", "Process I/O", "إدخال/إخراج العملية"),
        ("bk.read_cum", "bk", "خواندن (تجمعی)", "Read (cumulative)", "قراءة (تراكمية)"),
        ("bk.write_cum", "bk", "نوشتن (تجمعی)", "Write (cumulative)", "كتابة (تراكمية)"),
        ("bk.io_sub", "bk", "نرخ از اختلاف نمونه‌های متوالی بایت‌های تجمعی فرایند محاسبه می‌شود.", "Rates are derived from consecutive samples of process cumulative bytes.", "تُشتق المعدلات من عينات متتالية لبايتات العملية التراكمية."),
        ("bk.io_na", "bk", "I/O در دسترس نیست", "I/O N/A", "إ/خ غير متاح"),

        ("bk.policy", "bk", "سیاست (RPO / RTO)", "Policy (RPO / RTO)", "السياسة (RPO / RTO)"),
        ("bk.scheduled", "bk", "زمان‌بندی‌شده", "Scheduled", "مجدول"),
        ("bk.on", "bk", "روشن", "On", "تشغيل"),
        ("bk.off", "bk", "خاموش", "Off", "إيقاف"),
        ("bk.interval_rpo", "bk", "بازه (RPO)", "Interval (RPO)", "الفاصل (RPO)"),
        ("bk.target_rto", "bk", "هدف RTO", "Target RTO", "هدف RTO"),
        ("bk.retention", "bk", "نگه‌داشت", "Retention", "الاحتفاظ"),
        ("bk.retention_fmt", "bk", "{0} روز · حداکثر {1} فایل", "{0} days · max {1} files", "{0} يوماً · بحد أقصى {1} ملفاً"),
        ("bk.hours_abbr", "bk", "س", "h", "س"),
        ("bk.minutes_abbr", "bk", "دقیقه", "min", "د"),
        ("bk.enforce_retention", "bk", "اعمال نگه‌داشت الان", "Enforce retention now", "تطبيق الاحتفاظ الآن"),
        ("bk.dr_runbook", "bk", "راهنمای بازیابی از حادثه ←", "DR runbook →", "دليل التعافي ←"),

        ("bk.archive", "bk", "آرشیو پشتیبان", "Backup archive", "أرشيف النسخ الاحتياطي"),
        ("bk.files_count", "bk", "{0} فایل", "{0} files", "{0} ملف"),
        ("bk.empty", "bk", "هنوز بکاپی نیست. یک ZIP کامل بسازید تا آرشیو پر شود.", "No backups yet. Create a full zip to populate the archive.", "لا توجد نسخ بعد. أنشئ ZIP كاملاً لملء الأرشيف."),
        ("bk.col_file", "bk", "فایل", "File", "الملف"),
        ("bk.col_kind", "bk", "نوع", "Kind", "النوع"),
        ("bk.col_size", "bk", "حجم", "Size", "الحجم"),
        ("bk.col_created", "bk", "ایجاد (UTC)", "Created (UTC)", "أُنشئ (UTC)"),
        ("bk.download", "bk", "دانلود", "Download", "تنزيل"),
        ("bk.delete", "bk", "حذف", "Delete", "حذف"),
        ("bk.confirm_delete", "bk", "این بکاپ حذف شود؟", "Delete this backup?", "هل تريد حذف هذه النسخة؟"),

        ("bk.flash_ready", "bk", "بکاپ آماده است: {0} ({1})", "Backup ready: {0} ({1})", "النسخة جاهزة: {0} ({1})"),
        ("bk.flash_failed", "bk", "بکاپ ناموفق: {0}", "Backup failed: {0}", "فشل النسخ: {0}"),
        ("bk.flash_missing", "bk", "فایل بکاپ روی دیسک پیدا نشد.", "Backup file not found on disk.", "ملف النسخة غير موجود على القرص."),
        ("bk.flash_deleted", "bk", "بکاپ حذف شد.", "Backup deleted.", "تم حذف النسخة."),
        ("bk.flash_not_found", "bk", "بکاپ پیدا نشد.", "Backup not found.", "النسخة غير موجودة."),
        ("bk.flash_purged", "bk", "{0} بکاپ قدیمی پاک شد.", "Purged {0} old backup(s).", "تم حذف {0} من النسخ القديمة."),
        ("bk.flash_purge_none", "bk", "چیزی برای پاک‌سازی نبود.", "Nothing to purge.", "لا يوجد ما يُحذف."),
    };
}
