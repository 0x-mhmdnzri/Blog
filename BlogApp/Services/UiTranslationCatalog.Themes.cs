namespace BlogApp.Services;

public static partial class UiTranslationCatalog
{
    /// <summary>AdminThemes Index + Create (FA / EN / AR).</summary>
    public static readonly (string Key, string Group, string Fa, string En, string Ar)[] Themes =
    {
        ("themes.title", "themes", "تم‌ها", "Themes", "السمات"),
        ("themes.create", "themes", "تم جدید", "New theme", "سمة جديدة"),
        ("themes.all", "themes", "همه", "All", "الكل"),
        ("themes.pending", "themes", "در انتظار", "Pending", "قيد الانتظار"),
        ("themes.approved", "themes", "تأییدشده", "Approved", "موافق عليه"),
        ("themes.rejected", "themes", "رد شده", "Rejected", "مرفوض"),
        ("themes.draft", "themes", "پیش‌نویس", "Draft", "مسودة"),
        ("themes.pending_review", "themes", "در انتظار تأیید", "Pending review", "بانتظار الموافقة"),

        ("themes.super_hint", "themes", "سوپرادمین: پیش‌نمایش روی سایت → تأیید / رد / فعال‌سازی. نویسندگان فقط تم خود را می‌سازند.", "SuperAdmin: preview on site → approve / reject / activate. Authors only create and upload their own.", "المشرف: معاينة على الموقع → موافقة / رفض / تفعيل."),
        ("themes.author_hint", "themes", "شما می‌توانید تم بسازید یا فایل .blogtheme آپلود کنید. پس از تأیید سوپرادمین در گالری عمومی ظاهر می‌شود.", "You can create a theme or upload a .blogtheme file. After SuperAdmin approval it appears in the public gallery.", "يمكنك إنشاء سمة أو رفع ملف .blogtheme. بعد موافقة المشرف تظهر في المعرض."),
        ("themes.contrast_hint", "themes", "حداقل کنتراست متن روی پس‌زمینه ۴٫۵:۱ و اکسنت ۳:۱.", "Min text contrast 4.5:1 on background, accent 3:1.", "تباين النص 4.5:1 واللون المميز 3:1."),

        ("themes.upload", "themes", "آپلود تم شخصی (.blogtheme)", "Upload personal theme (.blogtheme)", "رفع سمة شخصية (.blogtheme)"),
        ("themes.upload_submit", "themes", "آپلود و ارسال برای تأیید", "Upload & submit for review", "رفع وإرسال للمراجعة"),
        ("themes.upload_draft", "themes", "آپلود پیش‌نویس", "Upload as draft", "رفع كمسودة"),
        ("themes.import_system", "themes", "واردات سیستمی (تأیید خودکار)", "System import (auto-approve)", "استيراد النظام (موافقة تلقائية)"),
        ("themes.import_btn", "themes", "واردات سیستم", "Import system", "استيراد النظام"),
        ("themes.scan_folder", "themes", "اسکن پوشه themes", "Scan themes folder", "مسح مجلد السمات"),
        ("themes.scan_title", "themes", "اسکن مجدد themes/*.blogtheme", "Rescan themes/*.blogtheme", "إعادة مسح themes/*.blogtheme"),

        ("themes.empty", "themes", "تمی نیست. «تم جدید» یا آپلود فایل را امتحان کنید.", "No themes. Try «New theme» or upload a file.", "لا سمات."),
        ("themes.active_site", "themes", "فعال سایت", "Site active", "نشط للموقع"),
        ("themes.system", "themes", "سیستم", "System", "نظام"),

        ("themes.approve", "themes", "تأیید", "Approve", "موافقة"),
        ("themes.reject", "themes", "رد", "Reject", "رفض"),
        ("themes.reject_reason", "themes", "دلیل رد (اختیاری)", "Reject reason (optional)", "سبب الرفض (اختياري)"),
        ("themes.activate", "themes", "فعال‌سازی سایت", "Activate site theme", "تفعيل سمة الموقع"),
        ("themes.submit_review", "themes", "ارسال برای تأیید", "Submit for review", "إرسال للمراجعة"),
        ("themes.delete", "themes", "حذف", "Delete", "حذف"),
        ("themes.delete_confirm", "themes", "حذف این تم؟", "Delete this theme?", "حذف هذه السمة؟"),
        ("themes.save_draft", "themes", "ذخیره پیش‌نویس", "Save draft", "حفظ مسودة"),
        ("themes.create_approve", "themes", "ساخت و تأیید", "Create & approve", "إنشاء وموافقة"),
        ("themes.back", "themes", "بازگشت", "Back", "رجوع"),

        ("themes.preview", "themes", "پیش‌نمایش روی سایت", "Preview on site", "معاينة على الموقع"),
        ("themes.preview_hint", "themes", "اعمال موقت این تم فقط برای مرورگر شما — بدون فعال‌سازی عمومی", "Temporarily apply this theme in your browser only — no public activation", "تطبيق مؤقت في متصفحك فقط دون تفعيل عام"),
        ("themes.end_preview", "themes", "پایان پیش‌نمایش", "End preview", "إنهاء المعاينة"),
        ("themes.preview_active", "themes", "حالت پیش‌نمایش", "Preview mode", "وضع المعاينة"),
        ("themes.preview_banner", "themes", "تم در حال بررسی روی این مرورگر اعمال شده. پس از ارزیابی کنتراست و ظاهر، تأیید یا رد کنید.", "This theme is applied on your browser for review. Check contrast and UI, then approve or reject.", "هذه السمة مطبّقة على متصفحك للمراجعة. افحص التباين ثم وافق أو ارفض."),

        ("themes.name", "themes", "نام", "Name", "الاسم"),
        ("themes.desc", "themes", "توضیح", "Description", "الوصف"),
        ("themes.color_bg", "themes", "پس‌زمینه", "Background", "الخلفية"),
        ("themes.color_surface", "themes", "سطح", "Surface", "السطح"),
        ("themes.color_surface2", "themes", "سطح ۲", "Surface 2", "سطح ٢"),
        ("themes.color_border", "themes", "حاشیه", "Border", "الحدود"),
        ("themes.color_text", "themes", "متن", "Text", "النص"),
        ("themes.color_text_muted", "themes", "متن کم‌رنگ", "Muted text", "نص باهت"),
        ("themes.color_accent", "themes", "اکسنت", "Accent", "مميز"),
        ("themes.color_danger", "themes", "خطر", "Danger", "خطر"),
        ("themes.color_success", "themes", "موفقیت", "Success", "نجاح"),
    };
}
