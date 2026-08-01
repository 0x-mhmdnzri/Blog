using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Primitives;

namespace AVICRM.Services.Performance;

/// <summary>Only cache anonymous GET responses (skip authenticated sessions).</summary>
public sealed class AnonymousGetOutputCachePolicy : IOutputCachePolicy
{
    public static readonly AnonymousGetOutputCachePolicy Instance = new();

    ValueTask IOutputCachePolicy.CacheRequestAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        var http = context.HttpContext;
        var ok = HttpMethods.IsGet(http.Request.Method)
                 && http.User.Identity?.IsAuthenticated != true;

        context.EnableOutputCaching = ok;
        context.AllowCacheLookup = ok;
        context.AllowCacheStorage = ok;
        context.AllowLocking = true;

        context.CacheVaryByRules.QueryKeys = "*";

        return ValueTask.CompletedTask;
    }

    ValueTask IOutputCachePolicy.ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellation)
        => ValueTask.CompletedTask;

    ValueTask IOutputCachePolicy.ServeResponseAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        var response = context.HttpContext.Response;
        if (response.StatusCode is not (200 or 301 or 302 or 404))
        {
            context.AllowCacheStorage = false;
        }
        if (!StringValues.IsNullOrEmpty(response.Headers.SetCookie))
        {
            context.AllowCacheStorage = false;
        }
        return ValueTask.CompletedTask;
    }
}
