# Developer Features — Single Web App Monolith

All features live in **one host project**: `BlogApp/`.
No multi-project runtime dependency is required to build or run.

```
BlogApp/                         # ASP.NET Core MVC monolith (composition root)
  Developer/
    Domain/                      # Domain events + AggregateRoot
    Messaging/                   # MassTransit publisher + consumers (EDD)
    Middleware/                  # Extension pipeline slots
    Plugins/                     # Plugin loader (drop DLLs in plugins/)
    Widgets/                     # Widget registry + built-ins
    Observability/               # App metrics meters
    DeveloperFeaturesBootstrap.cs
  Controllers, Services, Data, Views, …
plugins/                         # Optional third-party plugin DLLs
themes/                          # .blogtheme packs
```

## FEATURES.md → implementation

| Item | Implementation |
|------|----------------|
| **Event Bus** | MassTransit (`IBus`) — InMemory by default |
| **Domain Events** | `IDomainEvent` / `DomainEventBase` + `IDomainEventPublisher` |
| **EDD (MassTransit)** | Domain → Integration event → Consumer |
| **Plugin Architecture** | `IBlogPlugin` + `PluginLoader` (`plugins/*.dll`) |
| **Theme System** | `IThemeService` + `.blogtheme` packs |
| **Custom Widgets** | `IWidget` + `WidgetRegistry` → `GET /widgets/{zone}` |
| **Custom Middleware Pipeline** | `IPipelineExtension` slots: early / pre-auth / post-auth / pre-endpoint |
| **Extension SDK** | Implement `IBlogPlugin` in a class library referencing the host contracts |
| **Health Checks** | `GET /healthz` (live), `GET /healthz/ready` (SQLite) |
| **Metrics Endpoint** | `GET /metrics` (Prometheus) |
| **Distributed Tracing** | OpenTelemetry ASP.NET + HttpClient (+ OTLP if configured) |
| **Structured Logging** | Serilog (existing) |
| **OpenTelemetry** | `OpenTelemetry:*` config |

## MassTransit EDD flow

```
Controller / service
  → raise PostPublishedDomainEvent (etc.)
  → IDomainEventPublisher (MassTransitDomainEventPublisher)
  → maps to PostPublishedIntegrationEvent
  → IBus.Publish
  → PostPublishedConsumer (metrics + structured log)
```

**Transport**

| Config | Behavior |
|--------|----------|
| `RabbitMq:HostName` **empty** | MassTransit **InMemory** (single process, no broker) |
| `RabbitMq:HostName` set | MassTransit **RabbitMQ** |

```bash
# Optional broker
docker compose --profile rabbitmq up -d
# .env
RabbitMq__HostName=rabbitmq
```

## Publish an event from code

```csharp
await _events.PublishAsync(new PostPublishedDomainEvent(
    post.Id, post.Title, post.Slug, post.AuthorId, post.PublishedAtUtc!.Value));
```

Wired today on **Create** / **Edit** (publish / unpublish) in `PostsController`.

## Endpoints

| Path | Auth | Purpose |
|------|------|---------|
| `GET /health` | anon | Simple liveness |
| `GET /healthz` | anon | Tagged live check |
| `GET /healthz/ready` | anon | SQLite readiness |
| `GET /metrics` | anon | Prometheus scrape |
| `GET /widgets/{zone}` | anon | Render widgets (e.g. `sidebar`) |
| `GET /dev/plugins` | SuperAdmin | Loaded plugins |
| `GET /dev/bus` | SuperAdmin | MassTransit transport info |

## Run

```bash
cd BlogApp
dotnet restore
dotnet run
```

Solution file contains **only** `BlogApp` (true single-project monolith).
Legacy `src/**` folders may remain in the repo for reference but are **not** part of the build.
