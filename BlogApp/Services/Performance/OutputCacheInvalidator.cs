using Microsoft.AspNetCore.OutputCaching;

namespace BlogApp.Services.Performance;

public interface IOutputCacheInvalidator
{
    Task InvalidateHomeAsync(CancellationToken ct = default);
    Task InvalidatePostAsync(CancellationToken ct = default);
    Task InvalidateTaxonomyAsync(CancellationToken ct = default);
    Task InvalidateAllPublicAsync(CancellationToken ct = default);
}

/// <summary>Evicts ASP.NET Output Cache tags used by public HTML pages.</summary>
public sealed class OutputCacheInvalidator : IOutputCacheInvalidator
{
    public const string TagHome = "home";
    public const string TagPost = "post";
    public const string TagTaxonomy = "taxonomy";
    public const string TagPages = "pages";

    private readonly IOutputCacheStore _store;
    private readonly ILogger<OutputCacheInvalidator> _logger;

    public OutputCacheInvalidator(IOutputCacheStore store, ILogger<OutputCacheInvalidator> logger)
    {
        _store = store;
        _logger = logger;
    }

    public Task InvalidateHomeAsync(CancellationToken ct = default) => EvictAsync(TagHome, ct);
    public Task InvalidatePostAsync(CancellationToken ct = default) => EvictAsync(TagPost, ct);
    public Task InvalidateTaxonomyAsync(CancellationToken ct = default) => EvictAsync(TagTaxonomy, ct);

    public async Task InvalidateAllPublicAsync(CancellationToken ct = default)
    {
        await EvictAsync(TagHome, ct);
        await EvictAsync(TagPost, ct);
        await EvictAsync(TagTaxonomy, ct);
        await EvictAsync(TagPages, ct);
    }

    private async Task EvictAsync(string tag, CancellationToken ct)
    {
        try
        {
            await _store.EvictByTagAsync(tag, ct);
            _logger.LogDebug("Output cache tag evicted {Tag}", tag);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to evict output cache tag {Tag}", tag);
        }
    }
}
