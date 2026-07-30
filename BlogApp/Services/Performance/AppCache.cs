using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace BlogApp.Services.Performance;

public sealed class AppCache : IAppCache
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly IDistributedCache _cache;
    private readonly PerformanceOptions _opt;
    private readonly ConcurrentDictionary<string, byte> _keys = new(StringComparer.Ordinal);

    public AppCache(IDistributedCache cache, IOptions<PerformanceOptions> opt)
    {
        _cache = cache;
        _opt = opt.Value;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var bytes = await _cache.GetAsync(key, ct);
        if (bytes is null || bytes.Length == 0) return default;
        return JsonSerializer.Deserialize<T>(bytes, JsonOpts);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOpts);
        var opts = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl ?? TimeSpan.FromSeconds(Math.Max(5, _opt.Cache.DefaultSeconds))
        };
        await _cache.SetAsync(key, bytes, opts, ct);
        _keys[key] = 0;
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        await _cache.RemoveAsync(key, ct);
        _keys.TryRemove(key, out _);
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        foreach (var key in _keys.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
            {
                await _cache.RemoveAsync(key, ct);
                _keys.TryRemove(key, out _);
            }
        }
    }
}
