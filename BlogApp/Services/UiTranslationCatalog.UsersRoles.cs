namespace BlogApp.Services;

public static partial class UiTranslationCatalog
{
    public static readonly (string Key, string Group, string Fa, string En, string Ar)[] UsersRoles =
    {
        ("admin.nav.users", "admin", "مدیریت کاربران", "User management", "إدارة المستخدمين"),
        ("admin.nav.roles", "admin", "نقش‌ها و مجوزها", "Roles & permissions", "الأدوار والصلاحيات"),
        ("page.users", "page", "مدیریت کاربران", "User management", "إدارة المستخدمين"),
        ("page.roles", "page", "نقش‌ها و مجوزها", "Roles & permissions", "الأدوار والصلاحيات"),

        ("users.subtitle", "users", "مدیریت کاربران، نقش‌ها و دسترسی صفحات", "Manage users, roles and page access", "إدارة المستخدمين والأدوار وصلاحيات الصفحات"),
        ("users.btn_roles", "users", "نقش‌ها و مجوزها", "Roles & permissions", "الأدوار والصلاحيات"),
        ("users.btn_assign", "users", "اختصاص نقش", "Assign role", "تعيين دور"),
        ("users.status_active", "users", "فعال", "Active", "نشط"),
        ("users.status_locked", "users", "قفل‌شده", "Locked", "مقفل"),

        ("roles.page_title", "roles", "نقش‌ها و مجوزها", "Roles & permissions", "الأدوار والصلاحيات"),
        ("roles.subtitle", "roles", "سلسله‌مراتب پایه: SuperAdmin ← Author ← Reader — نقش سفارشی و claimها دسترسی صفحات را گسترش می‌دهند.", "Base hierarchy: SuperAdmin ← Author ← Reader — custom roles and claims extend page access.", "التسلسل: SuperAdmin ← Author ← Reader — الأدوار المخصصة والـ claims توسّع الوصول."),
        ("roles.btn_assign", "roles", "اختصاص به کاربر", "Assign to user", "تعيين لمستخدم"),
        ("roles.btn_users", "roles", "کاربران", "Users", "المستخدمون"),
        ("roles.label_new", "roles", "نقش جدید", "New role", "دور جديد"),
        ("roles.btn_create", "roles", "ایجاد نقش", "Create role", "إنشاء دور"),
        ("roles.builtin", "roles", "سیستمی", "Built-in", "نظامي"),
        ("roles.meta_pages", "roles", "صفحه", "pages", "صفحات"),
        ("roles.meta_caps", "roles", "قابلیت", "capabilities", "قدرات"),
        ("roles.meta_users", "roles", "کاربر", "users", "مستخدمون"),
        ("roles.btn_perms", "roles", "مجوزها", "Permissions", "الصلاحيات"),
        ("roles.btn_delete", "roles", "حذف", "Delete", "حذف"),
        ("roles.confirm_delete", "roles", "حذف نقش {0}؟", "Delete role {0}?", "حذف الدور {0}؟"),

        ("roles.assign_title", "roles", "اختصاص نقش به کاربر", "Assign roles to user", "تعيين أدوار لمستخدم"),
        ("roles.assign_sub", "roles", "چند نقش را همزمان انتخاب کنید؛ claimهای نقش‌ها با هم ترکیب (union) می‌شوند.", "Select multiple roles; role claims are combined (union).", "اختر عدة أدوار؛ تُدمج claims الأدوار معًا."),
        ("roles.back_roles", "roles", "← نقش‌ها", "← Roles", "← الأدوار"),
        ("roles.label_user", "roles", "کاربر", "User", "المستخدم"),
        ("roles.ph_select", "roles", "— انتخاب —", "— Select —", "— اختر —"),
        ("roles.btn_save_roles", "roles", "ذخیره نقش‌ها", "Save roles", "حفظ الأدوار"),
        ("roles.btn_user_list", "roles", "لیست کاربران", "User list", "قائمة المستخدمين"),

        ("roles.perm_title", "roles", "مجوزهای نقش {0}", "Permissions for {0}", "صلاحيات الدور {0}"),
        ("roles.perm_sub", "roles", "درخت منوی ادمین از سرور ساخته شده — صفحات را تیک بزنید و قابلیت‌ها را ترکیب کنید.", "Admin menu tree is built on the server — check pages and combine capabilities.", "شجرة قائمة الإدارة من الخادم — علّم الصفحات واجمع القدرات."),
        ("roles.perm_super_note", "roles", "SuperAdmin همیشه همه دسترسی‌ها را دارد", "SuperAdmin always has full access", "SuperAdmin لديه كل الصلاحيات دائمًا"),
        ("roles.perm_select_all", "roles", "انتخاب همه", "Select all", "تحديد الكل"),
        ("roles.perm_clear", "roles", "پاک کردن", "Clear", "مسح"),
        ("roles.perm_pages", "roles", "صفحات پنل ادمین", "Admin panel pages", "صفحات لوحة الإدارة"),
        ("roles.perm_pages_hint", "roles", "منو و زیرمنو · تولید خودکار", "Menu & submenu · auto-generated", "القائمة والفرعية · تلقائي"),
        ("roles.perm_caps", "roles", "قابلیت‌ها (Claims)", "Capabilities (Claims)", "القدرات (Claims)"),
        ("roles.perm_caps_hint", "roles", "ترکیب با صفحات برای دسترسی ریزدانه‌تر", "Combine with pages for finer access", "اجمعها مع الصفحات لوصول أدق"),
        ("roles.perm_save", "roles", "ذخیره مجوزها", "Save permissions", "حفظ الصلاحيات"),
        ("roles.perm_cancel", "roles", "انصراف", "Cancel", "إلغاء"),
        ("roles.perm_relogin", "roles", "کاربران پس از ذخیره باید یک‌بار خارج و دوباره وارد شوند.", "Users must sign out and back in after saving.", "يجب على المستخدمين تسجيل الخروج ثم الدخول بعد الحفظ."),
    };
}
