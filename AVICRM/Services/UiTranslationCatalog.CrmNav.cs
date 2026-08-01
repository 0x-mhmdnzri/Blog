namespace AVICRM.Services;

/// <summary>AVICRM admin sidebar — FA / EN / AR (FEATURES.md CRM modules).</summary>
public static partial class UiTranslationCatalog
{
    public static readonly (string Key, string Group, string Fa, string En, string Ar)[] CrmNav =
    {
        ("crm.nav.aria", "crm", "منوی مدیریت", "Admin menu", "قائمة الإدارة"),
        ("crm.nav.dashboard", "crm", "داشبورد", "Dashboard", "لوحة التحكم"),

        ("crm.nav.group.core", "crm", "هسته CRM", "CRM Core", "نواة CRM"),
        ("crm.nav.contacts", "crm", "مخاطبین", "Contacts", "جهات الاتصال"),
        ("crm.nav.accounts", "crm", "حساب‌ها / شرکت‌ها", "Accounts", "الحسابات"),
        ("crm.nav.leads", "crm", "سرنخ‌ها", "Leads", "العملاء المحتملون"),
        ("crm.nav.opportunities", "crm", "فرصت‌ها", "Opportunities", "الفرص"),
        ("crm.nav.activities", "crm", "فعالیت‌ها", "Activities", "الأنشطة"),

        ("crm.nav.group.sales", "crm", "فروش و پایپ‌لاین", "Sales & pipeline", "المبيعات والمسار"),
        ("crm.nav.pipeline", "crm", "پایپ‌لاین", "Pipeline", "المسار"),
        ("crm.nav.forecast", "crm", "پیش‌بینی", "Forecast", "التوقعات"),

        ("crm.nav.group.comm", "crm", "ارتباطات", "Communication", "التواصل"),
        ("crm.nav.email", "crm", "ایمیل", "Email", "البريد"),
        ("crm.nav.tasks", "crm", "وظایف", "Tasks", "المهام"),

        ("crm.nav.group.service", "crm", "خدمات مشتری", "Customer service", "خدمة العملاء"),
        ("crm.nav.cases", "crm", "تیکت‌ها", "Cases", "التذاكر"),
        ("crm.nav.kb", "crm", "دانش‌نامه", "Knowledge base", "قاعدة المعرفة"),

        ("crm.nav.group.marketing", "crm", "بازاریابی", "Marketing", "التسويق"),
        ("crm.nav.campaigns", "crm", "کمپین‌ها", "Campaigns", "الحملات"),
        ("crm.nav.lists", "crm", "لیست‌ها", "Lists", "القوائم"),

        ("crm.nav.group.automation", "crm", "اتوماسیون", "Automation", "الأتمتة"),
        ("crm.nav.workflows", "crm", "گردش‌کارها", "Workflows", "سير العمل"),

        ("crm.nav.group.analytics", "crm", "تحلیل و گزارش", "Analytics", "التحليلات"),
        ("crm.nav.analytics", "crm", "تحلیل‌ها", "Analytics", "التحليلات"),
        ("crm.nav.search", "crm", "جستجوی ادمین", "Admin search", "بحث الإدارة"),

        ("crm.nav.group.integrations", "crm", "یکپارچه‌سازی", "Integrations", "التكاملات"),
        ("crm.nav.apikeys", "crm", "کلیدهای API", "API keys", "مفاتيح API"),
        ("crm.nav.myapikeys", "crm", "کلیدهای من", "My API keys", "مفاتيحي"),

        ("crm.nav.group.security", "crm", "امنیت و انطباق", "Security & compliance", "الأمن والامتثال"),
        ("crm.nav.users", "crm", "کاربران", "Users", "المستخدمون"),
        ("crm.nav.roles", "crm", "نقش‌ها و مجوزها", "Roles & permissions", "الأدوار والصلاحيات"),
        ("crm.nav.audit", "crm", "گزارش ممیزی", "Audit log", "سجل التدقيق"),
        ("crm.nav.enterprise", "crm", "سازمانی", "Enterprise", "المؤسسة"),

        ("crm.nav.group.platform", "crm", "پلتفرم", "Platform", "المنصة"),
        ("crm.nav.settings", "crm", "تنظیمات", "Settings", "الإعدادات"),
        ("crm.nav.flags", "crm", "پرچم‌های قابلیت", "Feature flags", "أعلام الميزات"),
        ("crm.nav.backup", "crm", "پشتیبان و ذخیره‌سازی", "Backup & storage", "النسخ والتخزين"),
        ("crm.nav.jobs", "crm", "کارهای پس‌زمینه", "Background jobs", "المهام الخلفية"),
        ("crm.nav.notifications", "crm", "اعلان‌ها", "Notifications", "الإشعارات"),
        ("crm.nav.a11y", "crm", "دسترسی‌پذیری", "Accessibility", "إمكانية الوصول"),
        ("crm.nav.ai", "crm", "هوش مصنوعی", "AI (2026)", "الذكاء الاصطناعي"),
        ("crm.nav.profile", "crm", "پروفایل", "Profile", "الملف الشخصي"),

        ("admin.demo", "admin", "به‌زودی", "Soon", "قريباً"),
        ("admin.super_only", "admin", "مدیر ارشد", "SuperAdmin", "مسؤول أعلى"),
    };
}
