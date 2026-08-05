namespace BlogApp.Services;

public static partial class UiTranslationCatalog
{
    public static readonly (string Key, string Group, string Fa, string En, string Ar)[] Report =
    {
        ("report.trigger", "report", "گزارش تخلف", "Report", "إبلاغ"),
        ("report.eyebrow", "report", "ایمنی محتوا", "Content safety", "سلامة المحتوى"),
        ("report.title", "report", "گزارش این نوشته", "Report this post", "الإبلاغ عن هذا المنشور"),
        ("report.subtitle", "report", "اگر این محتوا نامناسب است، دلیل را انتخاب کنید.", "If this content is inappropriate, choose a reason.", "إذا كان هذا المحتوى غير مناسب، اختر سببًا."),
        ("report.pending_note", "report", "گزارش در وضعیت «در انتظار بررسی» برای نویسنده و مدیر ثبت می‌شود. نوشته حذف یا مخفی نمی‌شود.", "Your report is queued as pending for the author and admin. The post is not deleted or hidden.", "يُسجَّل بلاغك بحالة «قيد المراجعة» للمؤلف والمدير. المنشور لا يُحذف ولا يُخفى."),
        ("report.close", "report", "بستن", "Close", "إغلاق"),
        ("report.login_required", "report", "برای ارسال گزارش باید وارد حساب خود شوید.", "Sign in to submit a report.", "سجّل الدخول لإرسال بلاغ."),
        ("report.login_cta", "report", "ورود و ادامه", "Sign in to continue", "تسجيل الدخول والمتابعة"),
        ("report.reason_label", "report", "دلیل", "Reason", "السبب"),
        ("report.reason_required", "report", "لطفاً یک دلیل انتخاب کنید.", "Please choose a reason.", "يرجى اختيار سبب."),
        ("report.reason.spam", "report", "هرزنامه / تبلیغات", "Spam / ads", "بريد مزعج / إعلانات"),
        ("report.reason.harassment", "report", "توهین یا آزار", "Harassment", "تحرش أو إساءة"),
        ("report.reason.misinfo", "report", "اطلاعات نادرست", "Misinformation", "معلومات مضللة"),
        ("report.reason.copyright", "report", "نقض حق نشر", "Copyright", "انتهاك حقوق النشر"),
        ("report.reason.other", "report", "سایر", "Other", "أخرى"),
        ("report.details_label", "report", "توضیح (اختیاری)", "Details (optional)", "تفاصيل (اختياري)"),
        ("report.details_placeholder", "report", "اگر نکته‌ای هست بنویسید…", "Add any extra context…", "أضف أي سياق إضافي…"),
        ("report.details_hint", "report", "حداکثر ۱۰۰۰ نویسه", "Up to 1000 characters", "حتى 1000 حرف"),
        ("report.cancel", "report", "انصراف", "Cancel", "إلغاء"),
        ("report.submit", "report", "ارسال گزارش", "Submit report", "إرسال البلاغ"),
        ("report.success_title", "report", "گزارش ثبت شد", "Report submitted", "تم إرسال البلاغ"),
        ("report.info_title", "report", "پیام", "Notice", "تنبيه"),
        ("post.status_pending_review", "post", "در انتظار تأیید مدیر", "Pending admin approval", "بانتظار موافقة المدير"),
        ("post.created_pending_hint", "post", "نوشته شما ثبت شد و پس از تأیید مدیر منتشر می‌شود.", "Your post was saved and will go live after admin approval.", "تم حفظ مقالك وسيُنشر بعد موافقة المدير."),
    };
}
