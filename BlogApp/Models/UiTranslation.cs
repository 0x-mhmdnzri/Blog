using System.ComponentModel.DataAnnotations;

namespace BlogApp.Models;

/// <summary>
/// Parrot / UI translator row: one key × one language → translated chrome string.
/// Post body / title / summary are NEVER stored here — only labels, buttons, nav, messages.
/// </summary>
public class UiTranslation
{
    public int Id { get; set; }

    /// <summary>Stable key, e.g. admin.nav.dashboard</summary>
    [Required, MaxLength(160)]
    public string Key { get; set; } = string.Empty;

    /// <summary>ISO 639-1: fa, en, ar</summary>
    [Required, MaxLength(8)]
    public string LanguageCode { get; set; } = AppCultures.Default;

    [Required, MaxLength(2000)]
    public string Value { get; set; } = string.Empty;

    /// <summary>Optional group for admin translation editor (nav, button, form, message).</summary>
    [MaxLength(64)]
    public string? Group { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
