namespace BlogApp.Services;

public static partial class UiTranslationCatalog
{
    public static readonly (string Key, string Group, string Fa, string En, string Ar)[] Settings =
    {
        ("settings.seo_hint", "form", "این مقادیر در پایگاه داده ذخیره می‌شوند و روی متاتگ‌ها و JSON-LD هر صفحه اثر می‌گذارند (نه در appsettings یا .env).", "These values are stored in the database and affect meta tags and JSON-LD on every page (not appsettings or .env).", "تُحفظ هذه القيم في قاعدة البيانات وتؤثر على الوسوم وJSON-LD (وليس appsettings)."),
        ("settings.section_smtp", "form", "SMTP / ایمیل", "SMTP / Email", "SMTP / البريد"),
        ("settings.smtp_hint", "form", "تنظیمات ارسال ایمیل (خبرنامه، اعلان‌ها). فقط SuperAdmin. پس از ذخیره از دیتابیس خوانده می‌شود.", "Email sending settings (newsletter, notifications). SuperAdmin only. Loaded from the database after save.", "إعدادات إرسال البريد. للمشرف الأعلى فقط. تُقرأ من قاعدة البيانات بعد الحفظ."),
        ("settings.smtp_enable", "form", "فعال‌سازی SMTP", "Enable SMTP", "تفعيل SMTP"),
        ("settings.smtp_host", "form", "میزبان (Host)", "Host", "المضيف"),
        ("settings.smtp_port", "form", "پورت", "Port", "المنفذ"),
        ("settings.smtp_ssl", "form", "SSL / TLS", "SSL / TLS", "SSL / TLS"),
        ("settings.smtp_user", "form", "نام کاربری", "Username", "اسم المستخدم"),
        ("settings.smtp_password", "form", "رمز عبور", "Password", "كلمة المرور"),
        ("settings.smtp_password_placeholder", "form", "••••••••  (خالی = بدون تغییر)", "••••••••  (empty = unchanged)", "••••••••  (فارغ = دون تغيير)"),
        ("settings.smtp_password_hint", "form", "رمز قبلاً ذخیره شده؛ برای تغییر مقدار جدید وارد کنید.", "Password already saved; enter a new value to change it.", "كلمة المرور محفوظة؛ أدخل قيمة جديدة لتغييرها."),
        ("settings.smtp_from", "form", "آدرس فرستنده", "From address", "عنوان المرسل"),
        ("settings.smtp_from_name", "form", "نام نمایشی فرستنده", "From display name", "اسم المرسل الظاهر"),
        ("settings.announcement_hint", "form", "اعلان در تمام صفحات عمومی نمایش داده می‌شود و کاربر می‌تواند آن را ببندد. با تغییر متن یا استایل، دوباره برای همه نمایش داده می‌شود.", "The announcement appears on all public pages and can be dismissed. Changing text or style shows it again for everyone.", "يظهر الإعلان في كل الصفحات العامة ويمكن إغلاقه. تغيير النص أو النمط يعيده للجميع."),
    };
}
