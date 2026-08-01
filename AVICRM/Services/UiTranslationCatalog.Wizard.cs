namespace AVICRM.Services;

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
        ("wizard.bio_optional", "wizard", "بیو (اختیاری)", "Bio (optional)", "نبذة (اختياري)"),
        ("wizard.username_help", "wizard", "۳ تا ۵۰ نویسه: حروف، عدد، . _ -", "3–50 chars: letters, numbers, . _ -", "٣–٥٠ حرفاً: أحرف وأرقام . _ -"),
        ("wizard.password_help", "wizard", "حداقل ۱۰ نویسه با حرف بزرگ، کوچک و عدد", "Min 10 chars with upper, lower, and a digit", "١٠ أحرف على الأقل مع حرف كبير وصغير ورقم"),
        ("wizard.author_step1_hint", "wizard", "نام کاربری، ایمیل و نام نمایشی نویسنده را وارد کنید.", "Enter the author’s username, email, and display name.", "أدخل اسم المستخدم والبريد والاسم المعروض للمؤلف."),
        ("wizard.author_step2_hint", "wizard", "بیو کوتاه اختیاری است و بعداً قابل ویرایش است.", "A short bio is optional and can be edited later.", "نبذة قصيرة اختيارية ويمكن تعديلها لاحقاً."),
        ("wizard.author_step3_hint", "wizard", "رمز عبور امن تنظیم کنید و موارد را مرور کنید.", "Set a strong password and review the details.", "عيّن كلمة مرور قوية وراجع التفاصيل."),
        ("wizard.notif_step1_hint", "wizard", "عنوان و متن اعلان را بنویسید. لینک اختیاری است.", "Write the notification title and body. Link is optional.", "اكتب عنوان ونص الإشعار. الرابط اختياري."),
        ("wizard.notif_step2_hint", "wizard", "مخاطب را انتخاب کنید؛ فیلدهای مرتبط ظاهر می‌شوند.", "Choose the audience; related fields appear as needed.", "اختر الجمهور؛ تظهر الحقول ذات الصلة حسب الحاجة."),
        ("wizard.notif_step3_hint", "wizard", "همین حالا بفرستید یا برای بعد زمان‌بندی کنید.", "Send now or schedule for later.", "أرسل الآن أو جدول لاحقاً."),
        ("wizard.leave_blank_self", "wizard", "خالی = خودتان", "Leave blank = yourself", "اتركه فارغاً = أنت"),
    };
}
