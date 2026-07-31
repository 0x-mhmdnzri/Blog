using System.Text.Json.Serialization;

namespace BlogApp.Models;

/// <summary>
/// Portable theme pack (.blogtheme). JSON file placed under ContentRoot/themes/.
/// Auto-imported on startup; SuperAdmin can also upload via /AdminThemes.
/// </summary>
public sealed class ThemePack
{
    /// <summary>Stable id for upsert (e.g. material-deep-ocean). Falls back to file name.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "dark";

    [JsonPropertyName("bg")]
    public string Bg { get; set; } = "#0b0e14";

    [JsonPropertyName("surface")]
    public string Surface { get; set; } = "#12161f";

    [JsonPropertyName("surface2")]
    public string Surface2 { get; set; } = "#171c27";

    [JsonPropertyName("border")]
    public string Border { get; set; } = "#232838";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "#e6e9f0";

    [JsonPropertyName("textMuted")]
    public string TextMuted { get; set; } = "#8b93a7";

    [JsonPropertyName("accent")]
    public string Accent { get; set; } = "#e3b341";

    [JsonPropertyName("danger")]
    public string Danger { get; set; } = "#e5637a";

    [JsonPropertyName("success")]
    public string Success { get; set; } = "#9ecb8c";

    /// <summary>If true, mark as system (non-deletable by users).</summary>
    [JsonPropertyName("isSystem")]
    public bool IsSystem { get; set; } = true;

    /// <summary>If true and contrast OK, activate after import (only one active).</summary>
    [JsonPropertyName("activate")]
    public bool Activate { get; set; }
}
