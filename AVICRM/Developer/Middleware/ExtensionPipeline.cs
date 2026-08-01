namespace AVICRM.Developer.Middleware;

/// <summary>Plugin-extensible middleware slot (early | pre-auth | post-auth | pre-endpoint).</summary>
public interface IPipelineExtension
{
    string Id { get; }
    string Slot { get; }
    int Order { get; }
    Task InvokeAsync(HttpContext context, RequestDelegate next);
}

public sealed class PipelineExtensionRegistry
{
    private readonly List<IPipelineExtension> _items = new();

    public void Register(IPipelineExtension extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        _items.RemoveAll(x => string.Equals(x.Id, extension.Id, StringComparison.OrdinalIgnoreCase));
        _items.Add(extension);
    }

    public IEnumerable<IPipelineExtension> ForSlot(string slot) =>
        _items.Where(x => string.Equals(x.Slot, slot, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Order);
}

public static class ExtensionPipelineExtensions
{
    public static IApplicationBuilder UseBlogExtensionSlot(this IApplicationBuilder app, string slot)
    {
        var registry = app.ApplicationServices.GetService<PipelineExtensionRegistry>();
        if (registry is null) return app;

        foreach (var ext in registry.ForSlot(slot))
        {
            var captured = ext;
            app.Use(async (ctx, next) => await captured.InvokeAsync(ctx, next));
        }

        return app;
    }
}
