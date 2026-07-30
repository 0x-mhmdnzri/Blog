namespace BlogApp.Services;

public static partial class UiTranslationCatalog
{
    public static readonly (string Key, string Group, string Fa, string En, string Ar)[] Errors =
    {
        ("err.home", "err", "بازگشت به خانه", "Back to home", "العودة للرئيسية"),
        ("err.back", "err", "بازگشت", "Go back", "رجوع"),
        ("err.login", "err", "ورود", "Sign in", "تسجيل الدخول"),
        ("err.support", "err", "اگر مشکل ادامه داشت با پشتیبانی تماس بگیرید.", "If this keeps happening, contact support.", "إذا استمرت المشكلة تواصل مع الدعم."),

        ("err.400.title", "err", "درخواست نامعتبر", "Bad request", "طلب غير صالح"),
        ("err.400.msg", "err", "درخواست شما قابل پردازش نیست. لطفاً دوباره تلاش کنید.", "Your request could not be processed. Please try again.", "تعذر معالجة طلبك. حاول مرة أخرى."),

        ("err.401.title", "err", "نیاز به ورود", "Sign in required", "يلزم تسجيل الدخول"),
        ("err.401.msg", "err", "برای ادامه باید وارد حساب کاربری شوید.", "You need to sign in to continue.", "يجب تسجيل الدخول للمتابعة."),

        ("err.403.title", "err", "دسترسی غیرمجاز", "Access denied", "الوصول مرفوض"),
        ("err.403.msg", "err", "شما مجوز لازم برای مشاهده این صفحه را ندارید.", "You do not have permission to view this page.", "ليست لديك صلاحية لعرض هذه الصفحة."),

        ("err.404.title", "err", "صفحه پیدا نشد", "Page not found", "الصفحة غير موجودة"),
        ("err.404.msg", "err", "آدرس وارد شده وجود ندارد یا جابه‌جا شده است.", "This address does not exist or has been moved.", "هذا العنوان غير موجود أو تم نقله."),

        ("err.405.title", "err", "روش غیرمجاز", "Method not allowed", "طريقة غير مسموحة"),
        ("err.405.msg", "err", "این عملیات برای این مسیر پشتیبانی نمی‌شود.", "This operation is not supported for this route.", "هذه العملية غير مدعومة لهذا المسار."),

        ("err.408.title", "err", "مهلت درخواست تمام شد", "Request timeout", "انتهت مهلة الطلب"),
        ("err.408.msg", "err", "سرور به‌موقع پاسخ نداد. دوباره تلاش کنید.", "The server took too long to respond. Try again.", "استغرق الخادم وقتًا طويلاً. حاول مجددًا."),

        ("err.429.title", "err", "درخواست‌های زیاد", "Too many requests", "طلبات كثيرة جدًا"),
        ("err.429.msg", "err", "کمی صبر کنید و دوباره تلاش کنید.", "Please slow down and try again shortly.", "يرجى الانتظار قليلاً ثم المحاولة."),

        ("err.500.title", "err", "خطای سرور", "Server error", "خطأ في الخادم"),
        ("err.500.msg", "err", "مشکلی در سرور رخ داد. به‌زودی برطرف می‌شود.", "Something went wrong on our side. We are looking into it.", "حدث خطأ من جهتنا. نعمل على إصلاحه."),

        ("err.502.title", "err", "دروازه نامعتبر", "Bad gateway", "بوابة غير صالحة"),
        ("err.502.msg", "err", "ارتباط با سرویس بالادستی برقرار نشد.", "Could not reach an upstream service.", "تعذر الوصول إلى خدمة أعلى."),

        ("err.503.title", "err", "سرویس در دسترس نیست", "Service unavailable", "الخدمة غير متاحة"),
        ("err.503.msg", "err", "سایت موقتاً در دسترس نیست. کمی بعد دوباره بیایید.", "The site is temporarily unavailable. Please try again later.", "الموقع غير متاح مؤقتًا. حاول لاحقًا."),

        ("err.generic.title", "err", "خطا", "Error", "خطأ"),
        ("err.generic.msg", "err", "درخواست شما قابل انجام نبود.", "Your request could not be completed.", "تعذر إكمال طلبك."),
    };
}
