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
    };
}
