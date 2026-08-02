using System.ComponentModel.DataAnnotations;

namespace AVICRM.Models;

/// <summary>
/// Hierarchical admin menu row (seeded from AdminNavCatalog / FEATURES.md).
/// Multi-lang labels live in UiTranslations (LabelKey).
/// </summary>
public class NavMenuItem
{
    public int Id { get; set; }

    [Required, MaxLength(80)]
    public string Key { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string LabelKey { get; set; } = string.Empty;

    public int? ParentId { get; set; }
    public NavMenuItem? Parent { get; set; }
    public ICollection<NavMenuItem> Children { get; set; } = new List<NavMenuItem>();

    [MaxLength(80)]
    public string? Controller { get; set; }

    [MaxLength(80)]
    public string? Action { get; set; }

    [MaxLength(400)]
    public string? IconPath { get; set; }

    public int SortOrder { get; set; }
    public bool IsSection { get; set; }
    public bool SuperAdminOnly { get; set; }
    public bool StaffOnly { get; set; }
    public bool DemoTag { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
