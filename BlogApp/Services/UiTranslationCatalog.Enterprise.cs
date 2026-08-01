namespace BlogApp.Services;

/// <summary>SuperAdmin Enterprise console — FA / EN / AR.</summary>
public static partial class UiTranslationCatalog
{
    public static readonly (string Key, string Group, string Fa, string En, string Ar)[] Enterprise =
    {
        ("admin.nav.enterprise", "admin", "سازمانی", "Enterprise", "المؤسسة"),

        ("ent.eyebrow", "ent", "مدیر ارشد · چندمستأجری و انطباق", "SuperAdmin · Multi-tenant & compliance", "المسؤول الأعلى · متعدد المستأجرين والامتثال"),
        ("ent.title", "ent", "کنسول سازمانی", "Enterprise console", "وحدة تحكم المؤسسة"),
        ("ent.lead", "ent",
            "مستأجرها، فضاهای کاری، دامنه، SSO، گردش تأیید، نگهداری قانونی، GDPR و بکاپ — در یک نمای واحد.",
            "Tenants, workspaces, domains, SSO, approval workflow, legal hold, GDPR and backups — in one place.",
            "المستأجرون ومساحات العمل والنطاقات وSSO ومسار الموافقة والحجز القانوني وGDPR والنسخ — في مكان واحد."),

        ("ent.open_backup", "ent", "پشتیبان و ذخیره‌سازی", "Backup & storage", "النسخ والتخزين"),
        ("ent.dr_runbook", "ent", "راهنمای بازیابی ←", "DR runbook →", "دليل التعافي ←"),

        ("ent.kpi_tenants", "ent", "مستأجرها", "Tenants", "المستأجرون"),
        ("ent.kpi_workspaces", "ent", "فضاهای کاری", "Workspaces", "مساحات العمل"),
        ("ent.kpi_approvals", "ent", "تأییدهای معلق", "Pending approvals", "موافقات معلّقة"),
        ("ent.kpi_backups", "ent", "بکاپ‌ها", "Backups", "النسخ"),

        ("ent.tab_tenants", "ent", "مستأجرها", "Tenants", "المستأجرون"),
        ("ent.tab_sso", "ent", "SSO", "SSO", "SSO"),
        ("ent.tab_approvals", "ent", "تأیید", "Approvals", "الموافقات"),
        ("ent.tab_compliance", "ent", "انطباق", "Compliance", "الامتثال"),
        ("ent.tab_backup", "ent", "بکاپ", "Backup", "النسخ"),
        ("ent.tab_i18n", "ent", "بومی‌سازی", "Localization", "التعريب"),

        ("ent.tenants_title", "ent", "مستأجرها و فضاهای کاری", "Tenants & workspaces", "المستأجرون ومساحات العمل"),
        ("ent.tenants_sub", "ent", "ایزوله‌سازی داده و دامنه سفارشی برای هر مستأجر.", "Data isolation and custom domains per tenant.", "عزل البيانات ونطاقات مخصصة لكل مستأجر."),
        ("ent.tenants_empty", "ent", "هنوز مستأجری نیست. اولین را بسازید.", "No tenants yet. Create the first one.", "لا مستأجرين بعد. أنشئ الأول."),
        ("ent.ph_code", "ent", "کد", "code", "الرمز"),
        ("ent.ph_name", "ent", "نام", "name", "الاسم"),
        ("ent.ph_workspace", "ent", "فضای کاری", "workspace", "مساحة العمل"),
        ("ent.add_tenant", "ent", "افزودن مستأجر", "Add tenant", "إضافة مستأجر"),
        ("ent.add_workspace", "ent", "فضای کاری", "workspace", "مساحة"),
        ("ent.add_domain", "ent", "دامنه", "domain", "نطاق"),
        ("ent.domains", "ent", "دامنه", "domains", "نطاقات"),
        ("ent.isolated", "ent", "ایزوله", "isolated", "معزول"),
        ("ent.shared", "ent", "اشتراکی", "shared", "مشترك"),
        ("ent.verified", "ent", "تأییدشده", "verified", "موثّق"),
        ("ent.pending", "ent", "در انتظار", "pending", "قيد الانتظار"),

        ("ent.sso_title", "ent", "SSO (OIDC / SAML)", "SSO (OIDC / SAML)", "SSO (OIDC / SAML)"),
        ("ent.sso_sub", "ent", "پیکربینی ارائه‌دهنده هویت. ثبت OpenIdConnect در استقرار انجام شود.", "Configure the identity provider. Wire OpenIdConnect at deploy time.", "اضبط مزود الهوية. اربط OpenIdConnect عند النشر."),
        ("ent.ph_display", "ent", "نام نمایشی", "Display name", "الاسم المعروض"),
        ("ent.ph_authority", "ent", "Authority / آدرس IdP", "Authority / IdP URL", "Authority / رابط IdP"),
        ("ent.ph_metadata", "ent", "Metadata URL (SAML)", "Metadata URL (SAML)", "Metadata URL (SAML)"),
        ("ent.sso_enabled", "ent", "فعال", "Enabled", "مفعّل"),
        ("ent.save_sso", "ent", "ذخیره SSO", "Save SSO", "حفظ SSO"),

        ("ent.approvals_title", "ent", "گردش تأیید محتوا", "Content approval workflow", "مسار موافقة المحتوى"),
        ("ent.approvals_sub", "ent", "نویسندگان درخواست می‌دهند؛ مدیر بررسی و انتشار می‌کند.", "Authors submit; reviewers approve and publish.", "يقدّم المؤلفون؛ يراجع المديرون وينشرون."),
        ("ent.approvals_empty", "ent", "تأیید معلقی نیست.", "No pending approvals.", "لا موافقات معلّقة."),
        ("ent.approve", "ent", "تأیید", "Approve", "موافقة"),
        ("ent.reject", "ent", "رد", "Reject", "رفض"),

        ("ent.legal_title", "ent", "نگهداری قانونی", "Legal hold", "حجز قانوني"),
        ("ent.legal_sub", "ent", "از حذف دادهٔ مشمول پرونده جلوگیری می‌کند.", "Prevents purge of data under investigation.", "يمنع حذف البيانات قيد التحقيق."),
        ("ent.ph_post_id", "ent", "شناسه نوشته", "Post Id", "معرّف المقال"),
        ("ent.ph_user_id", "ent", "شناسه کاربر", "User Id", "معرّف المستخدم"),
        ("ent.ph_reason", "ent", "دلیل", "Reason", "السبب"),
        ("ent.place_hold", "ent", "اعمال نگهداری", "Place hold", "وضع الحجز"),

        ("ent.gdpr_title", "ent", "GDPR", "GDPR", "GDPR"),
        ("ent.gdpr_sub", "ent", "خروجی داده و پاک‌سازی / ناشناس‌سازی کاربر.", "Export subject data or erase / anonymize a user.", "تصدير بيانات الشخص أو محو / إخفاء هوية مستخدم."),
        ("ent.export_json", "ent", "خروجی JSON", "Export JSON", "تصدير JSON"),
        ("ent.erase", "ent", "پاک‌سازی", "Erase", "محو"),
        ("ent.confirm_erase", "ent", "این کاربر پاک / ناشناس شود؟", "Erase / anonymize this user?", "محو / إخفاء هوية هذا المستخدم؟"),

        ("ent.backup_title", "ent", "بکاپ و بازیابی", "Backup & restore", "النسخ والاستعادة"),
        ("ent.backup_sub", "ent", "اسنپ‌شات سریع از اینجا؛ مانیتورینگ کامل در صفحهٔ پشتیبان.", "Quick snapshot here; full monitoring on the Backup page.", "لقطة سريعة من هنا؛ المراقبة الكاملة في صفحة النسخ."),
        ("ent.create_backup", "ent", "ایجاد بکاپ", "Create backup", "إنشاء نسخة"),
        ("ent.backup_empty", "ent", "بکاپی ثبت نشده.", "No backups recorded.", "لا نسخ مسجّلة."),
        ("ent.stage_restore", "ent", "آماده‌سازی بازیابی", "Stage restore", "تجهيز الاستعادة"),

        ("ent.i18n_title", "ent", "بومی‌سازی سرمقاله‌ای", "Editorial localization", "تعريب تحريري"),
        ("ent.i18n_sub", "ent", "کلیدهای ترجمهٔ محتوا برای تیم تحریریه.", "Content translation keys for the editorial team.", "مفاتيح ترجمة المحتوى لفريق التحرير."),
        ("ent.i18n_empty", "ent", "ورودی بومی‌سازی نیست.", "No localization entries.", "لا إدخالات تعريب."),
    };
}
