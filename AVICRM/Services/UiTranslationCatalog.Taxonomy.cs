namespace AVICRM.Services;

public static partial class UiTranslationCatalog
{
    public static readonly (string Key, string Group, string Fa, string En, string Ar)[] Taxonomy =
    {
        ("tax.title", "tax", "دسته‌بندی‌ها و برچسب‌ها", "Categories & tags", "التصنيفات والوسوم"),

        ("tax.tab_cats", "tax", "دسته‌بندی‌ها", "Categories", "التصنيفات"),
        ("tax.tab_tags", "tax", "برچسب‌ها", "Tags", "الوسوم"),
        ("tax.tab_series", "tax", "سری‌ها", "Series", "السلاسل"),
        ("tax.tab_topics", "tax", "مجموعه‌های موضوعی", "Topic collections", "مجموعات المواضيع"),

        ("tax.cats_heading", "tax", "دسته‌بندی‌های تو در تو", "Nested categories", "تصنيفات متداخلة"),
        ("tax.tags_heading", "tax", "مدیریت برچسب‌ها", "Tag management", "إدارة الوسوم"),
        ("tax.series_heading", "tax", "مدیریت سری‌ها", "Series management", "إدارة السلاسل"),
        ("tax.topics_heading", "tax", "مجموعه‌های موضوعی", "Topic collections", "مجموعات المواضيع"),

        ("tax.label_cat_name", "tax", "نام دسته", "Category name", "اسم التصنيف"),
        ("tax.label_parent", "tax", "دسته والد", "Parent category", "التصنيف الأب"),
        ("tax.label_root", "tax", "— ریشه —", "— Root —", "— جذر —"),
        ("tax.label_description", "tax", "توضیح", "Description", "الوصف"),
        ("tax.label_tag_name", "tax", "نام برچسب", "Tag name", "اسم الوسم"),
        ("tax.label_series_name", "tax", "نام سری", "Series name", "اسم السلسلة"),
        ("tax.label_topic_name", "tax", "نام موضوع", "Topic name", "اسم الموضوع"),
        ("tax.label_merge_from", "tax", "ادغام از", "Merge from", "دمج من"),
        ("tax.label_merge_to", "tax", "به", "Into", "إلى"),

        ("tax.btn_add", "tax", "افزودن", "Add", "إضافة"),
        ("tax.btn_add_tag", "tax", "افزودن برچسب", "Add tag", "إضافة وسم"),
        ("tax.btn_create_series", "tax", "ایجاد سری", "Create series", "إنشاء سلسلة"),
        ("tax.btn_create_topic", "tax", "ایجاد موضوع", "Create topic", "إنشاء موضوع"),
        ("tax.btn_merge", "tax", "ادغام برچسب‌ها", "Merge tags", "دمج الوسوم"),
        ("tax.btn_view", "tax", "مشاهده", "View", "عرض"),
        ("tax.btn_edit", "tax", "ویرایش", "Edit", "تعديل"),
        ("tax.btn_delete", "tax", "حذف", "Delete", "حذف"),
        ("tax.btn_public", "tax", "صفحه عمومی", "Public page", "الصفحة العامة"),

        ("tax.col_name", "tax", "نام", "Name", "الاسم"),
        ("tax.col_slug", "tax", "اسلاگ", "Slug", "المعرّف"),
        ("tax.col_posts", "tax", "نوشته‌ها", "Posts", "المقالات"),
        ("tax.col_items", "tax", "آیتم‌ها", "Items", "العناصر"),
        ("tax.col_actions", "tax", "عملیات", "Actions", "إجراءات"),

        ("tax.empty_cats", "tax", "هنوز دسته‌ای ثبت نشده.", "No categories yet.", "لا تصنيفات بعد."),

        ("tax.confirm_delete_cat", "tax", "حذف این دسته؟", "Delete this category?", "حذف هذا التصنيف؟"),
        ("tax.confirm_delete_tag", "tax", "حذف برچسب؟", "Delete this tag?", "حذف هذا الوسم؟"),
        ("tax.confirm_delete_series", "tax", "حذف سری؟", "Delete this series?", "حذف هذه السلسلة؟"),
        ("tax.confirm_delete_topic", "tax", "حذف موضوع؟", "Delete this topic?", "حذف هذا الموضوع؟"),

        ("tax.msg_cat_added", "tax", "دسته افزوده شد.", "Category added.", "تمت إضافة التصنيف."),
        ("tax.msg_cat_deleted", "tax", "دسته حذف شد.", "Category deleted.", "تم حذف التصنيف."),
        ("tax.msg_cat_has_children", "tax", "ابتدا زیردسته‌ها را حذف یا جابه‌جا کنید.", "Remove or move child categories first.", "احذف أو انقل التصنيفات الفرعية أولاً."),
        ("tax.msg_tag_added", "tax", "برچسب افزوده شد.", "Tag added.", "تمت إضافة الوسم."),
        ("tax.msg_tag_deleted", "tax", "برچسب حذف شد.", "Tag deleted.", "تم حذف الوسم."),
        ("tax.msg_tags_merged", "tax", "برچسب‌ها ادغام شدند.", "Tags merged.", "تم دمج الوسوم."),
        ("tax.msg_series_created", "tax", "سری ایجاد شد.", "Series created.", "تم إنشاء السلسلة."),
        ("tax.msg_series_deleted", "tax", "سری حذف شد.", "Series deleted.", "تم حذف السلسلة."),
        ("tax.msg_topic_created", "tax", "مجموعه موضوعی ایجاد شد.", "Topic collection created.", "تم إنشاء مجموعة المواضيع."),
        ("tax.msg_topic_deleted", "tax", "مجموعه موضوعی حذف شد.", "Topic collection deleted.", "تم حذف مجموعة المواضيع."),

        ("tax.edit_series_title", "tax", "ویرایش سری", "Edit series", "تعديل السلسلة"),
        ("tax.edit_topic_title", "tax", "ویرایش موضوع", "Edit topic", "تعديل الموضوع"),
    };
}
