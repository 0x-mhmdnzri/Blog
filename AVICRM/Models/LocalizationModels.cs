using System.ComponentModel.DataAnnotations;

namespace AVICRM.Models;

/// <summary>Translation workflow status for a language version of a post.</summary>
public enum TranslationStatus
{
    /// <summary>Original language version (source of truth).</summary>
    Original = 0,
    /// <summary>Translation draft — not public yet.</summary>
    Draft = 1,
    /// <summary>Ready for editor/admin review.</summary>
    ReadyForReview = 2,
    /// <summary>Approved and can be published with the post.</summary>
    Approved = 3
}

/// <summary>Static catalog of cultures the site can serve.</summary>
public static class AppCultures
{
    public const string Default = "fa";

    public static readonly CultureDescriptor[] All =
    {
        new("fa", "fa-IR", "فارسی", "Persian",  true),
        new("en", "en-US", "English", "English", false),
        new("ar", "ar-SA", "العربية", "Arabic", true)
    };

    public static CultureDescriptor? Find(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var c = code.Trim().ToLowerInvariant();
        if (c.Length > 2) c = c[..2];
        return All.FirstOrDefault(x => x.Code == c);
    }

    public static bool IsSupported(string? code) => Find(code) is not null;

    public static string Normalize(string? code)
    {
        var d = Find(code);
        return d?.Code ?? Default;
    }
}

public sealed record CultureDescriptor(
    string Code,
    string Locale,
    string NativeName,
    string EnglishName,
    bool IsRtl)
{
    public string Direction => IsRtl ? "rtl" : "ltr";
    public string BootstrapCss => IsRtl
        ? "https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.rtl.min.css"
        : "https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css";
}

/// <summary>Sibling translation link shown on post details / editor.</summary>
public class PostTranslationLink
{
    public int PostId { get; set; }
    public string LanguageCode { get; set; } = AppCultures.Default;
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public TranslationStatus Status { get; set; }
    public string NativeName { get; set; } = string.Empty;
    public bool IsRtl { get; set; }
}
