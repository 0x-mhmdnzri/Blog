using Blog.Domain.Widgets;

namespace Blog.Application.Widgets;

public interface IWidgetRegistry
{
    void Register(IWidgetDescriptor widget);
    IReadOnlyList<IWidgetDescriptor> GetForZone(string zone);
    IReadOnlyList<IWidgetDescriptor> All { get; }
}

public sealed class WidgetRegistry : IWidgetRegistry
{
    private readonly List<IWidgetDescriptor> _widgets = new();

    public IReadOnlyList<IWidgetDescriptor> All => _widgets.AsReadOnly();

    public void Register(IWidgetDescriptor widget)
    {
        ArgumentNullException.ThrowIfNull(widget);
        _widgets.RemoveAll(w => string.Equals(w.Id, widget.Id, StringComparison.OrdinalIgnoreCase));
        _widgets.Add(widget);
    }

    public IReadOnlyList<IWidgetDescriptor> GetForZone(string zone)
    {
        if (string.IsNullOrWhiteSpace(zone)) return Array.Empty<IWidgetDescriptor>();
        return _widgets
            .Where(w => w.Zones.Any(z => string.Equals(z, zone, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(w => w.Order)
            .ThenBy(w => w.DisplayName)
            .ToList();
    }
}
