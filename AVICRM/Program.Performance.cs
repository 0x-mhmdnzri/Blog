using AVICRM.Services.AdminSearch;
using AVICRM.Services.Performance;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Caching.Distributed;

namespace AVICRM;

/// <summary>Performance DI registration extracted for clarity.</summary>
public static class PerformanceServiceExtensions
{
    public static IServiceCollection AddBlogPerformance(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<PerformanceOptions>(config.GetSection(PerformanceOptions.Section));
        var perf = config.GetSection(PerformanceOptions.Section).Get<PerformanceOptions>() ?? new PerformanceOptions();

        // Distributed cache: Redis when configured, otherwise in-memory distributed
        var provider = perf.Cache.Provider?.Trim().ToLowerInvariant() ?? "memory";
        if (provider == "redis" && !string.IsNullOrWhiteSpace(perf.Cache.RedisConnection))
        {
            services.AddStackExchangeRedisCache(o =>
            {
                o.Configuration = perf.Cache.RedisConnection;
                o.InstanceName = perf.Cache.RedisInstanceName;
            });
            Console.WriteLine($"[Performance] Distributed cache: Redis ({perf.Cache.RedisInstanceName})");
        }
        else
        {
            services.AddDistributedMemoryCache();
            Console.WriteLine("[Performance] Distributed cache: memory");
        }

        if (perf.Cache.ResponseCacheEnabled)
            services.AddResponseCaching();

        if (perf.Cache.OutputCacheEnabled)
        {
            services.AddOutputCache(options =>
            {
                options.AddBasePolicy(b => b
                    .AddPolicy(typeof(AnonymousGetOutputCachePolicy))
                    .Expire(TimeSpan.FromSeconds(Math.Max(5, perf.Cache.DefaultSeconds))));

                options.AddPolicy("home", b => b
                    .AddPolicy(typeof(AnonymousGetOutputCachePolicy))
                    .Expire(TimeSpan.FromSeconds(Math.Max(5, perf.Cache.HomeFeedSeconds)))
                    .SetVaryByQuery("category", "tag", "q", "page", "sort", "featured", "minRead")
                    .SetVaryByHeader("Accept-Language")
                    .Tag(OutputCacheInvalidator.TagHome, OutputCacheInvalidator.TagTaxonomy));

                options.AddPolicy("post", b => b
                    .AddPolicy(typeof(AnonymousGetOutputCachePolicy))
                    .Expire(TimeSpan.FromSeconds(Math.Max(10, perf.Cache.PostPageSeconds)))
                    .SetVaryByQuery("sort")
                    .SetVaryByHeader("Accept-Language")
                    .Tag(OutputCacheInvalidator.TagPost));

                options.AddPolicy("taxonomy", b => b
                    .AddPolicy(typeof(AnonymousGetOutputCachePolicy))
                    .Expire(TimeSpan.FromSeconds(Math.Max(10, perf.Cache.HomeFeedSeconds)))
                    .SetVaryByQuery("page", "sort")
                    .SetVaryByHeader("Accept-Language")
                    .Tag(OutputCacheInvalidator.TagTaxonomy, OutputCacheInvalidator.TagHome));

                options.AddPolicy("public-page", b => b
                    .AddPolicy(typeof(AnonymousGetOutputCachePolicy))
                    .Expire(TimeSpan.FromSeconds(Math.Max(10, perf.Cache.PostPageSeconds)))
                    .Tag(OutputCacheInvalidator.TagPages));
            });
        }

        services.AddSingleton<IAppCache, AppCache>();
        services.AddSingleton<ICdnUrlService, CdnUrlService>();
        services.AddSingleton<IOutputCacheInvalidator, OutputCacheInvalidator>();
        services.AddScoped<IBackgroundJobQueue, BackgroundJobQueue>();
        services.AddScoped<ImageOptimizeService>();
        services.AddScoped<SearchIndexService>();
        services.AddScoped<AdminSearchService>();
        services.AddHostedService<BackgroundJobWorker>();

        return services;
    }

    public static IApplicationBuilder UseBlogPerformance(this IApplicationBuilder app, IConfiguration config)
    {
        var perf = config.GetSection(PerformanceOptions.Section).Get<PerformanceOptions>() ?? new PerformanceOptions();

        if (perf.Cache.ResponseCacheEnabled)
            app.UseResponseCaching();

        if (perf.Cache.OutputCacheEnabled)
            app.UseOutputCache();

        return app;
    }
}
