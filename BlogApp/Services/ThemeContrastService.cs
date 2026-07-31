using System.Globalization;
using System.Text.RegularExpressions;
using BlogApp.Models;

namespace BlogApp.Services;

/// <summary>WCAG-oriented contrast checks to prevent unreadable / chaotic themes.</summary>
public static class ThemeContrastService
{
    private static readonly Regex HexRx = new(
        "^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{3})$",
        RegexOptions.Compiled);

    public const double MinTextOnBg = 4.5;
    public const double MinMutedOnBg = 3.0;
    public const double MinAccentOnBg = 3.0;

    public sealed record ValidationResult(
        bool Ok,
        IReadOnlyList<string> Errors,
        double TextOnBg,
        double MutedOnBg,
        double AccentOnBg);

    public static bool IsValidHex(string? hex) =>
        !string.IsNullOrWhiteSpace(hex) && HexRx.IsMatch(hex.Trim());

    public static string NormalizeHex(string hex)
    {
        hex = hex.Trim();
        if (hex.Length == 4) // #RGB
        {
            return "#" + string.Concat(hex[1], hex[1], hex[2], hex[2], hex[3], hex[3]).ToLowerInvariant();
        }
        return hex.ToLowerInvariant();
    }

    public static double RelativeLuminance(string hex)
    {
        hex = NormalizeHex(hex).TrimStart('#');
        var r = int.Parse(hex[..2], NumberStyles.HexNumber) / 255.0;
        var g = int.Parse(hex[2..4], NumberStyles.HexNumber) / 255.0;
        var b = int.Parse(hex[4..6], NumberStyles.HexNumber) / 255.0;
        static double Lin(double c) => c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        return 0.2126 * Lin(r) + 0.7152 * Lin(g) + 0.0722 * Lin(b);
    }

    public static double ContrastRatio(string hexA, string hexB)
    {
        var l1 = RelativeLuminance(hexA);
        var l2 = RelativeLuminance(hexB);
        var lighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);
        return (lighter + 0.05) / (darker + 0.05);
    }

    public static ValidationResult Validate(CustomTheme t)
    {
        var errors = new List<string>();
        var fields = new (string Name, string? Value)[]
        {
            ("Bg", t.Bg), ("Surface", t.Surface), ("Surface2", t.Surface2),
            ("Border", t.Border), ("Text", t.Text), ("TextMuted", t.TextMuted),
            ("Accent", t.Accent), ("Danger", t.Danger), ("Success", t.Success)
        };

        foreach (var (name, value) in fields)
        {
            if (!IsValidHex(value))
                errors.Add($"{name}: رنگ نامعتبر (فقط #RGB یا #RRGGBB).");
        }

        if (errors.Count > 0)
            return new ValidationResult(false, errors, 0, 0, 0);

        t.Bg = NormalizeHex(t.Bg);
        t.Surface = NormalizeHex(t.Surface);
        t.Surface2 = NormalizeHex(t.Surface2);
        t.Border = NormalizeHex(t.Border);
        t.Text = NormalizeHex(t.Text);
        t.TextMuted = NormalizeHex(t.TextMuted);
        t.Accent = NormalizeHex(t.Accent);
        t.Danger = NormalizeHex(t.Danger);
        t.Success = NormalizeHex(t.Success);

        var textOnBg = ContrastRatio(t.Text, t.Bg);
        var mutedOnBg = ContrastRatio(t.TextMuted, t.Bg);
        var accentOnBg = ContrastRatio(t.Accent, t.Bg);

        if (textOnBg < MinTextOnBg)
            errors.Add($"متن روی پس‌زمینه کنتراست {textOnBg:0.0}:1 دارد (حداقل {MinTextOnBg}:1).");
        if (mutedOnBg < MinMutedOnBg)
            errors.Add($"متن کم‌رنگ روی پس‌زمینه {mutedOnBg:0.0}:1 (حداقل {MinMutedOnBg}:1).");
        if (accentOnBg < MinAccentOnBg)
            errors.Add($"اکسنت روی پس‌زمینه {accentOnBg:0.0}:1 (حداقل {MinAccentOnBg}:1).");

        // Surface should not equal text (chaos)
        if (ContrastRatio(t.Text, t.Surface) < 3.0)
            errors.Add("متن روی Surface کنتراست کافی ندارد.");

        t.ContrastTextOnBg = Math.Round(textOnBg, 2);
        t.ContrastMutedOnBg = Math.Round(mutedOnBg, 2);
        t.ContrastAccentOnBg = Math.Round(accentOnBg, 2);

        // Auto mode from bg luminance
        t.Mode = RelativeLuminance(t.Bg) > 0.45 ? "light" : "dark";

        return new ValidationResult(errors.Count == 0, errors, t.ContrastTextOnBg, t.ContrastMutedOnBg, t.ContrastAccentOnBg);
    }

    public static string ToCssVariables(CustomTheme t)
    {
        var accentSoft = HexToRgba(t.Accent, 0.12);
        return
            $"--bg:{t.Bg};" +
            $"--surface:{t.Surface};" +
            $"--surface-2:{t.Surface2};" +
            $"--surface-3:{t.Surface2};" +
            $"--border:{t.Border};" +
            $"--border-soft:{t.Border};" +
            $"--text:{t.Text};" +
            $"--text-muted:{t.TextMuted};" +
            $"--text-faint:{t.TextMuted};" +
            $"--accent:{t.Accent};" +
            $"--accent-dim:{t.Accent};" +
            $"--accent-soft:{accentSoft};" +
            $"--danger:{t.Danger};" +
            $"--success:{t.Success};";
    }

    private static string HexToRgba(string hex, double a)
    {
        hex = NormalizeHex(hex).TrimStart('#');
        var r = int.Parse(hex[..2], NumberStyles.HexNumber);
        var g = int.Parse(hex[2..4], NumberStyles.HexNumber);
        var b = int.Parse(hex[4..6], NumberStyles.HexNumber);
        return $"rgba({r},{g},{b},{a.ToString(CultureInfo.InvariantCulture)})";
    }
}
