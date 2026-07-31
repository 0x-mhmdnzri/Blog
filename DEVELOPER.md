# Developer Features — Clean Architecture (Artix.API layout)

Layer names and responsibilities follow [Artix.API](https://github.com/Artix-Co/Artix.API).

## Project layout

```
src/
  Core/
    Blog.Core.Domain              # Aggregates, entities, value objects, domain events
    Blog.Core.Contract            # Commands/Queries, repository ports, handler interfaces, configs
    Blog.Core.ApplicationService  # Command/Query handlers, feature orchestration
    Blog.Core.DomainService       # Pure domain services (no IO)
  Infra/
    Blog.Infra.Sql                # EF Core, DbContext adapters, repository implementations
    Blog.Infra.Messaging          # In-process event bus, outbox-ready publisher
    Blog.Infra.Plugins            # Plugin loader, widget host, pipeline extensions
    Blog.Infra.Observability      # OpenTelemetry, health checks, metrics
  Utils/
    Blog.Utils                    # Shared helpers (slug, domain-event extensions)
  Blog.Extensions.Sdk             # Public SDK for third-party plugins
Presentation (host):
  BlogApp/                        # ASP.NET Core MVC host (composition root)
```

## Layer jobs (same as Artix)

| Layer | Job |
|-------|-----|
| **Core.Domain** | Rich domain model: `AggregateRoot`, `BaseEntity`, `IDomainEvent`, VOs. No infrastructure refs. |
| **Core.Contract** | Ports: `ICommand` / `IQuery`, repository interfaces, DTOs, options. Depends only on Domain. |
| **Core.ApplicationService** | Use-cases: handlers under `Features/{Name}/{Admin\|Client}/Commands\|Queries`. Depends on Contract + Domain. |
| **Core.DomainService** | Domain logic that needs multiple aggregates / rules (no EF). |
| **Infra.*** | Adapters: SQL, messaging, plugins, telemetry. Implements Contract interfaces. |
| **Utils** | Cross-cutting pure helpers. |
| **BlogApp** | Host: DI wiring, middleware, Razor/API controllers. |

## Dependency direction

```
BlogApp → Infra.* → ApplicationService → Contract → Domain
                 ↘ DomainService → Contract → Domain
Utils ← any layer (optional)
```

## FEATURES.md mapping

| Item | Where |
|------|--------|
| Domain Events + Aggregate Root | `Core.Domain` (`AggregateRoot`, `Post` aggregate) |
| Event Bus | `Core.Contract` ports + `Infra.Messaging` |
| Plugin Architecture | `Core.Contract` + `Infra.Plugins` + `Blog.Extensions.Sdk` |
| Widgets / Middleware slots | `Infra.Plugins` |
| Health / Metrics / OTel | `Infra.Observability` |
| Theme system | Host services + `.blogtheme` packs (Presentation) |

## Example — Post aggregate (EDD)

```csharp
var post = Post.Create("Title", Slug.Create("my-slug"), "# md", authorId);
post.Publish();
foreach (var e in post.DomainEvents)
    await eventPublisher.PublishAsync(e);
post.ClearDomainEvents();
```

## Write a plugin

1. Reference `Blog.Extensions.Sdk`
2. Implement `IBlogPlugin`
3. Drop DLL into `BlogApp/plugins/`
4. Restart host

## Endpoints

- `GET /healthz` — liveness
- `GET /healthz/ready` — readiness
- `GET /metrics` — Prometheus
- `GET /widgets/{zone}` — widget HTML
- `GET /dev/plugins` — SuperAdmin plugin list
