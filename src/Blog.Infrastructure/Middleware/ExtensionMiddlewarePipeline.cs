using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Blog.Infrastructure.Middleware;

/// <summary>
/// Custom middleware pipeline slots for extensions (before/after auth, after routing).
/// Plugins register <see cref="IPipelineExtension"/> implementations.
/// </summary>
public interface IPipelineExtension
{
    string Id { get; }
    /// <summary>early | pre-auth | post-auth | pre-endpoint</summary>
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

public static class ExtensionMiddlewarePipelineExtensions
{
    public static IApplicationBuilder UseBlogExtensionSlot(this IApplicationBuilder app, string slot)
    {
        var registry = app.ApplicationServices.GetService(typeof(PipelineExtensionRegistry)) as PipelineExtensionRegistry;
        if (registry is null) return app;

        foreach (var ext in registry.ForSlot(slot))
        {
            var captured = ext;
            app.Use(async (ctx, next) => await captured.InvokeAsync(ctx, next));
        }

        return app;
    }
}
