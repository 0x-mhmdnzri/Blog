# Performance (FEATURES.md)

Implemented:

| Item | Implementation |
|------|----------------|
| **Response Caching** | `AddResponseCaching` + `UseResponseCaching`; `[ResponseCache]` on media & search suggest |
| **Output Caching** | `AddOutputCache` policies `home`, `public-page`; tag-based eviction ready |
| **Redis Cache** | `Performance:Cache:Provider=redis` + `RedisConnection`; falls back to in-memory distributed cache |
| **CDN Support** | `ICdnUrlService` + `Performance:Cdn:BaseUrl` rewrites `/media/{id}` |
| **Image Optimization Pipeline** | `ImageOptimizeService` + background job `media.optimize` (JPEG EOI strip; extensible) |
| **Background Jobs** | `BackgroundJobs` table + `BackgroundJobWorker` (retry with backoff) |
| **Search Indexing Worker** | `SearchIndexEntries` + `SearchIndexService` + jobs `search.index_post` / `search.remove_post` |
| **Queue-based Email Delivery** | Job type `email.send` → `IEmailSender` via worker (not on request thread) |

## Config (`appsettings.json`)

```json
"Performance": {
  "Cache": {
    "Provider": "memory",
    "RedisConnection": "localhost:6379",
    "DefaultSeconds": 60,
    "HomeFeedSeconds": 30,
    "PostPageSeconds": 120,
    "OutputCacheEnabled": true,
    "ResponseCacheEnabled": true
  },
  "Cdn": {
    "Enabled": false,
    "BaseUrl": "https://cdn.example.com",
    "RewriteMediaPaths": true
  },
  "ImageOptimize": {
    "Enabled": true,
    "MaxWidth": 1920,
    "JpegQuality": 82,
    "PreferWebP": true
  },
  "Jobs": {
    "Enabled": true,
    "PollIntervalMs": 2000,
    "BatchSize": 8
  }
}
```

## Wire-up after publish/save

```csharp
await _jobs.EnqueueIndexPostAsync(post.Id);
await _jobs.EnqueueImageOptimizeAsync(mediaId);
await _jobs.EnqueueEmailAsync(to, subject, html);
```

## Already in place (prior work)

- Response compression (Brotli/Gzip)
- EF compiled queries + `AsNoTracking` default
- SQLite pooling
- Static files `max-age=604800,immutable`
- Script `defer` / lean Index projections
