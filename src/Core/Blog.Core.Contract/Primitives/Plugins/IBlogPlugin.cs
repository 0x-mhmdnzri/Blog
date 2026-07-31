namespace Blog.Core.Contract.Primitives.Plugins;

using Microsoft.Extensions.DependencyInjection;

public interface IBlogPlugin
{
    string Id { get; }
    string Name { get; }
    string Version { get; }
    void ConfigureServices(IServiceCollection services);
}

public interface IWidgetDescriptor
{
    string Id { get; }
    string Zone { get; }
    int Order { get; }
    Task<string> RenderHtmlAsync(CancellationToken cancellationToken = default);
}

public interface IPipelineExtension
{
    string Slot { get; } // early | pre-auth | post-auth | pre-endpoint
    int Order { get; }
    Func<Microsoft.AspNetCore.Http.HttpContext, Func<Task>, Task> Invoke { get; }
}
