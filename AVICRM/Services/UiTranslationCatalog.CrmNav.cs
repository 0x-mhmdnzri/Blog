namespace AVICRM.Services;

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
        ("crm.nav.import", "crm", "ورود / خروج داده", "Import / Export", "استيراد / تصدير"),

        ("crm.nav.group.sales", "crm", "فروش و پایپ‌لاین", "Sales & pipeline", "المبيعات والمسار"),
        ("crm.nav.pipeline", "crm", "پایپ‌لاین", "Pipeline", "المسار"),
        ("crm.nav.forecast", "crm", "پیش‌بینی فروش", "Forecast", "التوقعات"),
        ("crm.nav.quotes", "crm", "پیش‌فاکتور / پیشنهاد", "Quotes / Proposals", "عروض الأسعار"),
        ("crm.nav.products", "crm", "محصولات و قیمت", "Products & pricing", "المنتجات والأسعار"),

        ("crm.nav.group.comm", "crm", "ارتباطات", "Communication", "التواصل"),
        ("crm.nav.email", "crm", "ایمیل", "Email", "البريد"),
        ("crm.nav.templates", "crm", "قالب‌های ایمیل", "Email templates", "قوالب البريد"),
        ("crm.nav.tasks", "crm", "وظایف", "Tasks", "المهام"),
        ("crm.nav.calendar", "crm", "تقویم", "Calendar", "التقويم"),
        ("crm.nav.sequences", "crm", "توالی / کادنس", "Sequences", "التسلسلات"),

        ("crm.nav.group.service", "crm", "خدمات مشتری", "Customer service", "خدمة العملاء"),
        ("crm.nav.cases", "crm", "تیکت‌ها", "Cases", "التذاكر"),
        ("crm.nav.kb", "crm", "دانش‌نامه", "Knowledge base", "قاعدة المعرفة"),
        ("crm.nav.portal", "crm", "پورتال مشتری", "Customer portal", "بوابة العملاء"),
        ("crm.nav.sla", "crm", "SLA و ارتقا", "SLA & escalation", "اتفاقية الخدمة"),

        ("crm.nav.group.marketing", "crm", "بازاریابی", "Marketing", "التسويق"),
        ("crm.nav.campaigns", "crm", "کمپین‌ها", "Campaigns", "الحملات"),
        ("crm.nav.lists", "crm", "لیست‌ها و سگمنت", "Lists & segments", "القوائم والشرائح"),
        ("crm.nav.consent", "crm", "رضایت و ترجیحات", "Consent & preferences", "الموافقة والتفضيلات"),
        ("crm.nav.attribution", "crm", "انتساب کمپین", "Campaign attribution", "إسناد الحملات"),

        ("crm.nav.group.automation", "crm", "اتوماسیون", "Automation", "الأتمتة"),
        ("crm.nav.workflows", "crm", "گردش‌کارها", "Workflows", "سير العمل"),
        ("crm.nav.assignment", "crm", "قواعد تخصیص", "Assignment rules", "قواعد التعيين"),
        ("crm.nav.validation", "crm", "قوانین اعتبارسنجی", "Validation rules", "قواعد التحقق"),
        ("crm.nav.customfields", "crm", "فیلد و لایه‌بندی", "Custom fields & layouts", "الحقول والتخطيطات"),

        ("crm.nav.group.analytics", "crm", "تحلیل و گزارش", "Analytics & reports", "التحليلات والتقارير"),
        ("crm.nav.analytics", "crm", "تحلیل‌ها", "Analytics", "التحليلات"),
        ("crm.nav.reports", "crm", "گزارش‌ها", "Reports", "التقارير"),
        ("crm.nav.search", "crm", "جستجوی ادمین", "Admin search", "بحث الإدارة"),
        ("crm.nav.dataquality", "crm", "کیفیت داده", "Data quality", "جودة البيانات"),

        ("crm.nav.group.integrations", "crm", "یکپارچه‌سازی", "Integrations", "التكاملات"),
        ("crm.nav.apikeys", "crm", "کلیدهای API", "API keys", "مفاتيح API"),
        ("crm.nav.myapikeys", "crm", "کلیدهای من", "My API keys", "مفاتيحي"),
        ("crm.nav.webhooks", "crm", "وب‌هوک‌ها", "Webhooks", "الويب هوك"),
        ("crm.nav.connectors", "crm", "اتصال‌دهنده‌ها", "Connectors", "الموصلات"),
        ("crm.nav.sso", "crm", "SSO", "SSO (SAML/OAuth)", "تسجيل الدخول الموحد"),

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
        ("crm.nav.i18n", "crm", "زبان و منطقه زمانی", "Language & timezone", "اللغة والمنطقة الزمنية"),
        ("crm.nav.ai", "crm", "هوش مصنوعی", "AI", "الذكاء الاصطناعي"),
        ("crm.nav.profile", "crm", "پروفایل", "Profile", "الملف الشخصي"),

        ("crm.nav.group.ai", "crm", "هوش مصنوعی پیشرفته", "AI & advanced", "الذكاء الاصطناعي المتقدم"),
        ("crm.nav.ai_scoring", "crm", "امتیازدهی سرنخ", "Lead scoring", "تقييم العملاء المحتملين"),
        ("crm.nav.ai_forecast", "crm", "پیش‌بینی درآمد", "Revenue forecast", "توقعات الإيرادات"),
        ("crm.nav.ai_nba", "crm", "پیشنهاد اقدام بعدی", "Next-best-action", "أفضل إجراء تالي"),
        ("crm.nav.ai_notes", "crm", "یادداشت خودکار از تماس", "Auto notes from calls", "ملاحظات تلقائية"),
        ("crm.nav.ai_churn", "crm", "تشخیص ریزش", "Churn detection", "اكتشاف التسرب"),

        ("admin.demo", "admin", "به‌زودی", "Soon", "قريباً"),
        ("admin.super_only", "admin", "مدیر ارشد", "SuperAdmin", "مسؤول أعلى"),
    };
}
