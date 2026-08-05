namespace BlogApp.Services;

public static partial class UiTranslationCatalog
{
    public static readonly (string Key, string Group, string Fa, string En, string Ar)[] Taxonomy =
    {
        ("tax.subtitle", "tax", "دسته‌ها، برچسب‌ها، سری‌ها و موضوعات — ساختار محتوا را اینجا مدیریت کنید.", "Categories, tags, series and topics — manage content structure here.", "التصنيفات والوسوم والسلاسل والمواضيع — أدِر هيكل المحتوى هنا."),
        ("tax.stats_aria", "tax", "آمار ساختار", "Structure stats", "إحصاءات الهيكل"),
        ("tax.tabs_aria", "tax", "بخش‌های ساختار", "Structure sections", "أقسام الهيكل"),

        ("tax.cats_desc", "tax", "درخت دسته‌بندی با والد/فرزند. عمق با تورفتگی نشان داده می‌شود.", "Category tree with parent/child. Depth is shown by indentation.", "شجرة تصنيفات أب/ابن. العمق يظهر بالتحريك."),
        ("tax.tags_desc", "tax", "برچسب‌های تخت — ادغام برچسب‌های تکراری از همین‌جا.", "Flat tags — merge duplicates from here.", "وسوم مسطحة — ادمج المكررات من هنا."),
        ("tax.series_desc", "tax", "مجموعه‌های چندقسمتی — ترتیب و ویرایش از صفحه سری.", "Multi-part collections — order and edit from the series page.", "مجموعات متعددة الأجزاء — رتّب وعدّل من صفحة السلسلة."),
        ("tax.topics_desc", "tax", "موضوعات فراتر از دسته — برای خوشه‌بندی موضوعی محتوا.", "Topics beyond categories — for thematic content clusters.", "مواضيع أبعد من التصنيف — لتجميع المحتوى موضوعيًا."),

        ("tax.ph_cat_name", "tax", "نام دسته", "Category name", "اسم التصنيف"),
        ("tax.ph_tag_name", "tax", "نام برچسب", "Tag name", "اسم الوسم"),
        ("tax.ph_series_name", "tax", "نام سری", "Series name", "اسم السلسلة"),
        ("tax.ph_topic_name", "tax", "نام موضوع", "Topic name", "اسم الموضوع"),
        ("tax.ph_desc_short", "tax", "توضیح کوتاه (اختیاری)", "Short description (optional)", "وصف قصير (اختياري)"),
        ("tax.ph_desc", "tax", "توضیح (اختیاری)", "Description (optional)", "وصف (اختياري)"),
        ("tax.ph_search_cats", "tax", "جست‌وجو در دسته‌ها…", "Search categories…", "بحث في التصنيفات…"),
        ("tax.ph_search_tags", "tax", "جست‌وجو در برچسب‌ها…", "Search tags…", "بحث في الوسوم…"),

        ("tax.empty_cats_hint", "tax", "اولین دسته را از فرم بالا اضافه کنید.", "Add the first category from the form above.", "أضف أول تصنيف من النموذج أعلاه."),
        ("tax.empty_tags", "tax", "برچسبی نیست", "No tags yet", "لا وسوم بعد"),
        ("tax.empty_tags_hint", "tax", "اولین برچسب را اضافه کنید.", "Add the first tag.", "أضف أول وسم."),
        ("tax.empty_series", "tax", "سری‌ای نیست", "No series yet", "لا سلاسل بعد"),
        ("tax.empty_topics", "tax", "موضوعی نیست", "No topics yet", "لا مواضيع بعد"),

        ("tax.merge_title", "tax", "ادغام برچسب‌ها", "Merge tags", "دمج الوسوم"),
        ("tax.posts_n", "tax", "{0} پست", "{0} posts", "{0} مقالات"),
        ("tax.items_n", "tax", "{0} مورد", "{0} items", "{0} عناصر"),

        ("tax.series_eyebrow", "tax", "سری نوشته‌ها", "Post series", "سلسلة مقالات"),
        ("tax.series_empty", "tax", "هنوز نوشته‌ای در این سری منتشر نشده.", "No posts published in this series yet.", "لا مقالات منشورة في هذه السلسلة بعد."),
        ("tax.topic_eyebrow", "tax", "مجموعه موضوعی", "Topic collection", "مجموعة موضوعية"),
        ("tax.topic_empty", "tax", "نوشته‌ای برای این موضوع یافت نشد.", "No posts found for this topic.", "لا مقالات لهذا الموضوع."),

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
