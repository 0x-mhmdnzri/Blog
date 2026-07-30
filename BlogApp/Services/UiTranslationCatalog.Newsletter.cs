namespace BlogApp.Services;

public static partial class UiTranslationCatalog
{
    public static readonly (string Key, string Group, string Fa, string En, string Ar)[] Newsletter =
    {
        ("nl.title", "nl", "خبرنامه", "Newsletter", "النشرة"),
        ("nl.intro", "nl", "برای دریافت نوشته‌های جدید ایمیل خود را وارد کنید. تأیید دومرحله‌ای الزامی است.", "Enter your email to get new posts. Double opt-in is required.", "أدخل بريدك. التأكيد المزدوج مطلوب."),
        ("nl.name", "nl", "نام (اختیاری)", "Name (optional)", "الاسم (اختياري)"),
        ("nl.subscribe", "nl", "عضویت", "Subscribe", "اشترك"),
        ("nl.double_optin_hint", "nl", "پس از ثبت‌نام، لینک تأیید به ایمیل شما ارسال می‌شود.", "After signup, a confirmation link is emailed to you.", "بعد التسجيل يُرسل رابط التأكيد."),

        ("nl.confirm_title", "nl", "تأیید عضویت", "Confirm subscription", "تأكيد الاشتراك"),
        ("nl.unsub_title", "nl", "لغو عضویت", "Unsubscribe", "إلغاء الاشتراك"),
        ("nl.back_home", "nl", "بازگشت به خانه", "Back home", "العودة للرئيسية"),
        ("nl.resubscribe", "nl", "عضویت دوباره", "Subscribe again", "اشترك مجددًا"),

        ("nl.check_email", "nl", "ایمیل تأیید ارسال شد — صندوق ورودی را بررسی کنید.", "Confirmation email sent — check your inbox.", "تم إرسال التأكيد — راجع بريدك."),
        ("nl.already_subscribed", "nl", "این ایمیل از قبل عضو است.", "This email is already subscribed.", "هذا البريد مشترك مسبقًا."),
        ("nl.already_confirmed", "nl", "عضویت قبلاً تأیید شده است.", "Already confirmed.", "مؤكد مسبقًا."),
        ("nl.confirmed", "nl", "عضویت شما تأیید شد. سپاس!", "Subscription confirmed. Thank you!", "تم تأكيد اشتراكك. شكرًا!"),
        ("nl.unsubscribed", "nl", "از خبرنامه خارج شدید.", "You have been unsubscribed.", "تم إلغاء اشتراكك."),
        ("nl.err_email", "nl", "ایمیل نامعتبر است.", "Invalid email.", "بريد غير صالح."),
        ("nl.err_token", "nl", "لینک نامعتبر یا منقضی است.", "Invalid or expired link.", "رابط غير صالح."),

        ("nl.confirm_subject", "nl", "تأیید عضویت در خبرنامه", "Confirm your newsletter subscription", "أكد اشتراكك"),
        ("nl.confirm_body", "nl", "برای تأیید عضویت روی لینک زیر کلیک کنید:", "Click the link below to confirm:", "انقر للتأكيد:"),

        ("nl.tab_overview", "nl", "نمای کلی", "Overview", "نظرة عامة"),
        ("nl.tab_subscribers", "nl", "مشترکان", "Subscribers", "المشتركون"),
        ("nl.tab_segments", "nl", "سگمنت‌ها", "Segments", "الشرائح"),
        ("nl.tab_campaigns", "nl", "کمپین‌ها", "Campaigns", "الحملات"),

        ("nl.kpi_total", "nl", "کل", "Total", "الإجمالي"),
        ("nl.kpi_confirmed", "nl", "تأییدشده", "Confirmed", "مؤكد"),
        ("nl.kpi_pending", "nl", "در انتظار", "Pending", "معلّق"),
        ("nl.kpi_campaigns", "nl", "کمپین", "Campaigns", "حملات"),

        ("nl.overview_hint", "nl", "عضویت با double opt-in، سگمنت زبان/تگ، کمپین فوری و زمان‌بندی‌شده.", "Double opt-in, language/tag segments, immediate & scheduled campaigns.", "تأكيد مزدوج وشرائح وحملات."),
        ("nl.public_page", "nl", "صفحه عمومی عضویت", "Public subscribe page", "صفحة الاشتراك"),

        ("nl.status", "nl", "وضعیت", "Status", "الحالة"),
        ("nl.add_segment", "nl", "افزودن سگمنت", "Add segment", "إضافة شريحة"),
        ("nl.segment_name", "nl", "نام سگمنت", "Segment name", "اسم الشريحة"),
        ("nl.desc", "nl", "توضیح", "Description", "الوصف"),
        ("nl.save", "nl", "ذخیره", "Save", "حفظ"),
        ("nl.add_campaign", "nl", "کمپین جدید", "New campaign", "حملة جديدة"),
        ("nl.subject", "nl", "موضوع", "Subject", "الموضوع"),
        ("nl.all_confirmed", "nl", "همه تأییدشده‌ها", "All confirmed", "كل المؤكدين"),
        ("nl.schedule", "nl", "زمان‌بندی", "Schedule", "جدولة"),
        ("nl.send_now", "nl", "ارسال الان", "Send now", "أرسل الآن"),
        ("nl.cancel", "nl", "لغو", "Cancel", "إلغاء"),

        ("nl.saved_segment", "nl", "سگمنت ذخیره شد.", "Segment saved.", "تم حفظ الشريحة."),
        ("nl.campaign_saved", "nl", "کمپین ذخیره / زمان‌بندی شد.", "Campaign saved/scheduled.", "تم حفظ الحملة."),
        ("nl.campaign_sent", "nl", "کمپین ارسال شد.", "Campaign sent.", "تم إرسال الحملة."),
        ("nl.err_segment", "nl", "نام سگمنت الزامی است.", "Segment name is required.", "اسم الشريحة مطلوب."),
        ("nl.err_campaign", "nl", "موضوع و متن الزامی است.", "Subject and body are required.", "الموضوع والنص مطلوبان."),
    };
}
