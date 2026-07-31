# Developer Features — Pure monolith

**One project only: `BlogApp`.**  
No class libraries. No n-tier / Clean Architecture multi-project solution.  
Folders under `BlogApp/Developer/` are **organizational**, not separate assemblies.

```
BlogApp/                         # sole .csproj + composition root
  Developer/
    Domain/                      # IDomainEvent, AggregateRoot helpers
    Messaging/                   # MassTransit publisher + consumers
    Middleware/                  # extension pipeline slots
    Plugins/                     # PluginLoader
    Widgets/                     # WidgetRegistry
    Observability/               # meters
    DeveloperFeaturesBootstrap.cs
  Controllers, Services, Data, Views, …
```

## FEATURES.md mapping

| Item | In-process location |
|------|---------------------|
| Event Bus | MassTransit inside `BlogApp` |
| Domain Events | `Developer/Domain` |
| EDD | Domain → Integration event → Consumer |
| Plugins | `Developer/Plugins` + `plugins/*.dll` |
| Themes | `IThemeService` + `themes/*.blogtheme` |
| Widgets | `GET /widgets/{zone}` |
| Middleware slots | early / pre-auth / post-auth / pre-endpoint |
| Health | `/healthz`, `/healthz/ready` |
| Metrics | `/metrics` |
| Tracing | OpenTelemetry in host |
| Logging | Serilog |

## MassTransit

| `RabbitMq:HostName` | Transport |
|---------------------|-----------|
| empty | InMemory |
| set | RabbitMQ |

```csharp
await _events.PublishAsync(new PostPublishedDomainEvent(
    post.Id, post.Title, post.Slug, post.AuthorId, post.PublishedAtUtc!.Value));
```

## Endpoints

| Path | Notes |
|------|--------|
| `/healthz` | live |
| `/healthz/ready` | SQLite |
| `/metrics` | Prometheus |
| `/widgets/{zone}` | HTML widgets |
| `/dev/bus` | SuperAdmin — transport |
| `/dev/plugins` | SuperAdmin |

## Build

```bash
cd BlogApp && dotnet run
# or
dotnet build BlogApp.sln   # only BlogApp project
```
