# Developer Features (Clean Architecture)

Implements **FEATURES.md → Developer Features** with DDD building blocks and class-library layers.

## Solution layers

| Project | Role |
|---------|------|
| `src/Blog.Domain` | Aggregate roots, value objects, domain events, plugin/widget contracts |
| `src/Blog.Application` | Event bus contracts, dispatcher, plugin/widget registries |
| `src/Blog.Infrastructure` | In-process event bus, OpenTelemetry, health checks, plugin loader, pipeline extensions |
| `src/Blog.Extensions.Sdk` | Public SDK for third-party plugins |
| `BlogApp` | ASP.NET Core host (composition root) |

## FEATURES.md mapping

| Item | Implementation |
|------|----------------|
| Event Bus | `IDomainEventBus` + `InProcessDomainEventBus` |
| Domain Events | `IDomainEvent` / `DomainEventBase` + Post aggregate events |
| Plugin Architecture | `IBlogPlugin` + `PluginLoader` (`ContentRoot/plugins/*.dll`) |
| Theme System | Existing `.blogtheme` packs + CSS variables |
| Custom Widgets | `IWidgetDescriptor` + zones + `/widgets/{zone}` + `_WidgetZone` partial |
| Custom Middleware Pipeline | `IPipelineExtension` + slots `early` / `pre-auth` / `post-auth` / `pre-endpoint` |
| Extension SDK | `Blog.Extensions.Sdk` |
| Health Checks | `/healthz` (live), `/healthz/ready` (ready) |
| Metrics Endpoint | `/metrics` (Prometheus) |
| Distributed Tracing | OpenTelemetry ASP.NET + Http + ActivitySource `BlogApp` |
| Structured Logging | Existing Serilog |
| OpenTelemetry Integration | `AddBlogOpenTelemetry` (OTLP optional via config) |

## Domain example — Post aggregate

```csharp
var post = PostAggregate.Create("Title", "my-slug", "# md", authorId);
post.Publish();
var events = post.DequeueDomainEvents(); // PostCreated + PostPublished
await dispatcher.DispatchAsync(events);
```

## Config (`appsettings.json`)

```json
"OpenTelemetry": {
  "ServiceName": "BlogApp",
  "OtlpEndpoint": "",
  "Prometheus": true
}
```

## Write a plugin

1. Reference `Blog.Extensions.Sdk`
2. Implement `IBlogPlugin` (or `IBlogExtension`)
3. Build DLL → drop into `BlogApp/plugins/`
4. Restart — loaded automatically

## Endpoints

- `GET /healthz` — liveness
- `GET /healthz/ready` — readiness (event bus + plugins)
- `GET /metrics` — Prometheus scrape
- `GET /widgets/{zone}` — HTML fragments
- `GET /dev/plugins` — SuperAdmin plugin list
- `GET /dev/widgets` — SuperAdmin widget list
