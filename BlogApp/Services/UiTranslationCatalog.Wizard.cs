namespace BlogApp.Services;

public static partial class UiTranslationCatalog
{
    public static readonly (string Key, string Group, string Fa, string En, string Ar)[] Wizard =
    {
        ("wizard.identity", "wizard", "هویت", "Identity", "الهوية"),
        ("wizard.profile", "wizard", "پروفایل", "Profile", "الملف"),
        ("wizard.security", "wizard", "امنیت", "Security", "الأمان"),
        ("wizard.content", "wizard", "محتوا", "Content", "المحتوى"),
        ("wizard.timing", "wizard", "زمان‌بندی", "Timing", "التوقيت"),
        ("wizard.next", "wizard", "بعدی", "Next", "التالي"),
        ("wizard.prev", "wizard", "قبلی", "Back", "السابق"),
        ("wizard.review", "wizard", "مرور", "Review", "مراجعة"),
        ("wizard.confirm_password", "wizard", "تکرار رمز عبور", "Confirm password", "تأكيد كلمة المرور"),
        ("wizard.bio", "wizard", "بیوگرافی", "Bio", "نبذة"),
        ("wizard.gender", "wizard", "جنسیت", "Gender", "الجنس"),
        ("wizard.avatar", "wizard", "تصویر پروفایل", "Profile photo", "صورة الملف"),
        ("wizard.avatar_help", "wizard", "اختیاری · jpg/png/webp", "Optional · jpg/png/webp", "اختياري · jpg/png/webp"),
        ("wizard.website", "wizard", "وب‌سایت", "Website", "الموقع"),
        ("wizard.phone", "wizard", "تلفن", "Phone", "الهاتف"),
        ("wizard.bio_optional", "wizard", "بیو (اختیاری)", "Bio (optional)", "نبذة (اختياري)"),
        ("wizard.username_help", "wizard", "۳ تا ۵۰ نویسه: حروف، عدد، . _ -", "3–50 chars: letters, numbers, . _ -", "٣–٥٠ حرفاً: أحرف وأرقام . _ -"),
        ("wizard.password_help", "wizard", "حداقل ۱۰ نویسه · حرف بزرگ و کوچک · عدد · نماد", "Min 10 chars · upper & lower · digit · symbol", "١٠ أحرف على الأقل · كبير وصغير · رقم · رمز"),
        ("wizard.author_step1_hint", "wizard", "نام کاربری، ایمیل و نام نمایشی نویسنده را وارد کنید.", "Enter the author’s username, email, and display name.", "أدخل اسم المستخدم والبريد والاسم المعروض للمؤلف."),
        ("wizard.author_step2_hint", "wizard", "بیو کوتاه اختیاری است و بعداً قابل ویرایش است.", "A short bio is optional and can be edited later.", "نبذة قصيرة اختيارية ويمكن تعديلها لاحقاً."),
        ("wizard.author_step3_hint", "wizard", "رمز عبور امن تنظیم کنید و موارد را مرور کنید.", "Set a strong password and review the details.", "عيّن كلمة مرور قوية وراجع التفاصيل."),
        ("wizard.notif_step1_hint", "wizard", "عنوان و متن اعلان را بنویسید. لینک اختیاری است.", "Write the notification title and body. Link is optional.", "اكتب عنوان ونص الإشعار. الرابط اختياري."),
        ("wizard.notif_step2_hint", "wizard", "مخاطب را انتخاب کنید؛ فیلدهای مرتبط ظاهر می‌شوند.", "Choose the audience; related fields appear as needed.", "اختر الجمهور؛ تظهر الحقول ذات الصلة حسب الحاجة."),
        ("wizard.notif_step3_hint", "wizard", "همین حالا بفرستید یا برای بعد زمان‌بندی کنید.", "Send now or schedule for later.", "أرسل الآن أو جدول لاحقاً."),
        ("wizard.leave_blank_self", "wizard", "خالی = خودتان", "Leave blank = yourself", "اتركه فارغاً = أنت"),

        ("wizard.val_username_required", "wizard", "نام کاربری الزامی است", "Username is required", "اسم المستخدم مطلوب"),
        ("wizard.val_username_pattern", "wizard", "۳ تا ۵۰ نویسه: حروف، اعداد، . _ -", "3–50 chars: letters, numbers, . _ -", "٣–٥٠: أحرف وأرقام . _ -"),
        ("wizard.val_email_required", "wizard", "ایمیل الزامی است", "Email is required", "البريد مطلوب"),
        ("wizard.val_email_invalid", "wizard", "ایمیل نامعتبر است", "Invalid email address", "بريد غير صالح"),
        ("wizard.val_display_required", "wizard", "نام نمایشی الزامی است", "Display name is required", "الاسم المعروض مطلوب"),
        ("wizard.val_display_min", "wizard", "حداقل ۲ نویسه", "At least 2 characters", "حرفان على الأقل"),
        ("wizard.val_bio_max", "wizard", "حداکثر ۵۰۰ نویسه", "Max 500 characters", "حد أقصى ٥٠٠ حرف"),
        ("wizard.val_password_required", "wizard", "رمز عبور الزامی است", "Password is required", "كلمة المرور مطلوبة"),
        ("wizard.val_password_pattern", "wizard", "حداقل ۱۰ نویسه، شامل حرف بزرگ، کوچک، عدد و نماد", "Min 10 chars with upper, lower, digit and symbol", "١٠ أحرف على الأقل مع كبير وصغير ورقم ورمز"),
        ("wizard.val_confirm_required", "wizard", "تکرار رمز عبور الزامی است", "Confirm password is required", "تأكيد كلمة المرور مطلوب"),
        ("wizard.val_confirm_match", "wizard", "رمز عبور و تکرار آن یکسان نیستند", "Passwords do not match", "كلمتا المرور غير متطابقتين"),
    };
}
