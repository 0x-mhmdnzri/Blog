namespace BlogApp.Services;

public static partial class UiTranslationCatalog
{
    public static readonly (string Key, string Group, string Fa, string En, string Ar)[] FileUpload =
    {
        ("fu.select", "fu", "انتخاب فایل", "Select file", "اختيار ملف"),
        ("fu.drop_title", "fu", "فایل را اینجا رها کنید یا انتخاب کنید", "Drop files here or select", "أفلت الملفات هنا أو اختر"),
        ("fu.hint_media", "fu", "jpg/png/gif/webp تا ۸MB · mp4/webm تا ۲۰۰MB", "jpg/png/gif/webp up to 8MB · mp4/webm up to 200MB", "jpg/png/gif/webp حتى ٨MB · mp4/webm حتى ٢٠٠MB"),
        ("fu.hint_image", "fu", "jpg/png/webp · حداکثر ۲MB", "jpg/png/webp · max 2MB", "jpg/png/webp · حتى ٢MB"),
        ("fu.hint_theme", "fu", ".blogtheme / .json", ".blogtheme / .json", ".blogtheme / .json"),
        ("fu.hint_csv", "fu", "CSV · UTF-8", "CSV · UTF-8", "CSV · UTF-8"),
        ("fu.hint_editor", "fu", "image/* · video/*", "image/* · video/*", "image/* · video/*"),
        ("fu.invalid_type", "fu", "نوع فایل مجاز نیست", "File type not allowed", "نوع الملف غير مسموح"),
        ("fu.invalid_some", "fu", "برخی فایل‌ها به‌خاطر نوع نادیده گرفته شدند", "Some files were skipped (type not allowed)", "تم تجاهل بعض الملفات بسبب النوع"),
        ("fu.preview_image", "fu", "تصویر", "Image", "صورة"),
        ("fu.preview_video", "fu", "ویدیو", "Video", "فيديو"),
        ("fu.preview_file", "fu", "فایل", "File", "ملف"),
        ("fu.clear", "fu", "پاک کردن", "Clear", "مسح"),
    };
}
