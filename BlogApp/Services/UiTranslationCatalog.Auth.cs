namespace BlogApp.Services;

public static partial class UiTranslationCatalog
{
    public static readonly (string Key, string Group, string Fa, string En, string Ar)[] Auth =
    {
        ("auth.login.title", "auth", "ورود", "Sign in", "تسجيل الدخول"),
        ("auth.login.heading", "auth", "ورود", "Sign in", "تسجيل الدخول"),
        ("auth.login.username", "auth", "نام کاربری یا ایمیل", "Username or email", "اسم المستخدم أو البريد"),
        ("auth.login.password", "auth", "رمز عبور", "Password", "كلمة المرور"),
        ("auth.login.submit", "auth", "ورود", "Sign in", "تسجيل الدخول"),
        ("auth.login.oauth", "auth", "ورود اجتماعی (OAuth)", "Social sign-in (OAuth)", "تسجيل دخول اجتماعي"),
        ("auth.login.oauth_hint", "auth", "برای فعال‌سازی، کلیدهای Authentication:Google و Authentication:GitHub را در تنظیمات قرار دهید.", "To enable, set Authentication:Google and Authentication:GitHub in configuration.", "لتفعيله عيّن Authentication:Google و Authentication:GitHub."),
        ("auth.login.no_account", "auth", "حساب ندارید؟", "No account?", "ليس لديك حساب؟"),
        ("auth.login.register_link", "auth", "ثبت‌نام", "Register", "إنشاء حساب"),

        ("auth.register.title", "auth", "ثبت‌نام خواننده", "Reader registration", "تسجيل القارئ"),
        ("auth.register.heading", "auth", "عضویت خواننده", "Join as reader", "انضم كقارئ"),
        ("auth.register.lead", "auth", "برای نشان‌گذاری نوشته‌ها یک حساب ساده بسازید.", "Create a simple account to bookmark posts.", "أنشئ حسابًا بسيطًا لحفظ المقالات."),
        ("auth.register.submit", "auth", "ثبت‌نام", "Register", "تسجيل"),
        ("auth.register.password_hint", "auth", "حداقل ۱۰ نویسه، حروف بزرگ/کوچک، عدد و نماد", "At least 10 characters, upper/lower, number and symbol", "١٠ أحرف على الأقل مع أحرف وأرقام ورمز"),
        ("auth.register.has_account", "auth", "حساب دارید؟", "Already have an account?", "لديك حساب؟"),
        ("auth.register.login_link", "auth", "ورود", "Sign in", "تسجيل الدخول"),

        ("auth.profile.title", "auth", "پروفایل من", "My profile", "ملفي"),
        ("auth.profile.saved", "auth", "پروفایل ذخیره شد.", "Profile saved.", "تم حفظ الملف."),
        ("auth.profile.display_name", "auth", "نام نمایشی", "Display name", "الاسم الظاهر"),
        ("auth.profile.bio", "auth", "بیو", "Bio", "نبذة"),
        ("auth.profile.avatar", "auth", "تصویر پروفایل", "Profile photo", "صورة الملف"),
        ("auth.profile.remove_photo", "auth", "حذف تصویر فعلی", "Remove current photo", "إزالة الصورة الحالية"),
        ("auth.profile.email", "auth", "ایمیل", "Email", "البريد"),
        ("auth.profile.username", "auth", "نام کاربری", "Username", "اسم المستخدم"),
        ("auth.profile.gender", "auth", "جنسیت", "Gender", "الجنس"),
        ("auth.profile.gender_unspecified", "auth", "ترجیح می‌دهم نگویم", "Prefer not to say", "أفضل عدم الإفصاح"),
        ("auth.profile.gender_male", "auth", "مرد", "Male", "ذكر"),
        ("auth.profile.gender_female", "auth", "زن", "Female", "أنثى"),
        ("auth.profile.gender_other", "auth", "سایر", "Other", "أخرى"),
        ("auth.profile.social", "auth", "شبکه‌های اجتماعی", "Social links", "روابط اجتماعية"),
        ("auth.profile.public_page", "auth", "صفحه عمومی", "Public page", "الصفحة العامة"),
        ("auth.profile.save_btn", "auth", "ذخیره پروفایل", "Save profile", "حفظ الملف"),

        ("themes.title", "admin", "تم‌ها", "Themes", "السمات"),
        ("themes.create", "admin", "تم جدید", "New theme", "سمة جديدة"),
        ("themes.all", "admin", "همه", "All", "الكل"),
        ("themes.pending", "admin", "در انتظار", "Pending", "قيد الانتظار"),
        ("themes.approved", "admin", "تأییدشده", "Approved", "موافق عليه"),
        ("themes.upload", "admin", "آپلود تم شخصی (.blogtheme)", "Upload personal theme (.blogtheme)", "رفع سمة (.blogtheme)"),
        ("themes.super_hint", "admin", "سوپرادمین: تأیید / رد / فعال‌سازی سایت. نویسندگان فقط تم خود را می‌سازند و آپلود می‌کنند.", "SuperAdmin: approve / reject / activate site theme. Authors only create and upload their own.", "المشرف: موافقة/رفض/تفعيل. المؤلفون ينشئون ويرفعون فقط."),
        ("themes.author_hint", "admin", "شما می‌توانید تم بسازید یا فایل .blogtheme آپلود کنید. پس از تأیید سوپرادمین در گالری ظاهر می‌شود.", "You can create a theme or upload a .blogtheme file. After SuperAdmin approval it appears in the gallery.", "يمكنك إنشاء سمة أو رفع ملف. بعد موافقة المشرف تظهر في المعرض."),
        ("themes.empty", "admin", "تمی یافت نشد.", "No themes found.", "لا توجد سمات."),
        ("themes.name", "admin", "نام", "Name", "الاسم"),
        ("themes.desc", "admin", "توضیح", "Description", "الوصف"),
        ("themes.save_draft", "admin", "ذخیره پیش‌نویس", "Save draft", "حفظ مسودة"),
        ("themes.submit_review", "admin", "ارسال برای بررسی", "Submit for review", "إرسال للمراجعة"),
        ("themes.contrast_hint", "admin", "حداقل کنتراست متن روی پس‌زمینه ۴٫۵:۱ و اکسنت ۳:۱.", "Min text contrast 4.5:1 on background, accent 3:1.", "تباين النص 4.5:1 واللون المميز 3:1."),

        ("roles.title", "admin", "نقش‌ها", "Roles", "الأدوار"),
        ("roles.permissions", "admin", "دسترسی‌ها", "Permissions", "الصلاحيات"),
        ("roles.assign", "admin", "اختصاص نقش", "Assign role", "تعيين دور"),
        ("roles.users_count", "admin", "کاربران", "Users", "المستخدمون"),

        ("apikey.mine.title", "apikey", "کلیدهای API من", "My API keys", "مفاتيح API الخاصة بي"),
        ("apikey.admin.title", "apikey", "مدیریت کلیدهای API", "Manage API keys", "إدارة مفاتيح API"),
        ("apikey.create", "apikey", "درخواست کلید جدید", "Request new key", "طلب مفتاح جديد"),
        ("apikey.empty", "apikey", "هنوز کلیدی ندارید.", "You have no keys yet.", "لا توجد مفاتيح بعد."),
        ("apikey.lead", "apikey", "کلیدهای شخصی برای دسترسی برنامه‌ای به API.", "Personal keys for programmatic API access.", "مفاتيح شخصية للوصول البرمجي."),

        ("a11y.admin.title", "admin", "دسترسی‌پذیری سایت", "Site accessibility", "إمكانية وصول الموقع"),
        ("a11y.admin.lead", "admin", "بررسی و راهنمای دسترسی‌پذیری برای همه صفحات.", "Checks and guidance for accessibility across the site.", "فحوصات وإرشادات لإمكانية الوصول."),
    };
}
