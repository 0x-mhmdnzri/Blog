# Developer Features — Pure monolith

**One project only: `AVICRM`.**  
No class libraries. No n-tier multi-project solution.  
Folders under `AVICRM/Developer/` are **organizational**, not separate assemblies.

If a leftover `src/` folder appears in a clone, remove it:

```bash
bash scripts/purge-src.sh
# or: rm -rf src && git add -A && git commit -m 'chore: remove src/' && git push
```

## Layout

```
AVICRM/                         # sole .csproj + composition root
  Developer/
    Domain/                      # IDomainEvent, AggregateRoot, integration events
    Messaging/                   # MassTransit publisher + consumers (EDD)
    Middleware/                  # extension pipeline slots
    Plugins/                     # PluginLoader + IBlogPlugin
    Widgets/                     # WidgetRegistry + built-in widgets
    Observability/               # BlogMetrics (OpenTelemetry meters)
    Sdk/                         # Extension SDK (IBlogExtension)
    DeveloperFeaturesBootstrap.cs
  themes/*.blogtheme             # Theme system packs
  plugins/*.dll                  # optional drop-in plugins
```

## FEATURES.md — Developer Features checklist

| Item | Status | Location |
|------|--------|----------|
| **Event Bus** | Done | MassTransit (`AddDeveloperFeatures`) |
| **Domain Events** | Done | `Developer/Domain/DomainEvents.cs` |
| **Plugin Architecture** | Done | `Developer/Plugins` + `plugins/` |
| **Theme System** | Done | `IThemeService` + `themes/*.blogtheme` |
| **Custom Widgets** | Done | `WidgetRegistry`, `/widgets/{zone}`, `_WidgetZone` |
| **Custom Middleware Pipeline** | Done | slots: `early` / `pre-auth` / `post-auth` / `pre-endpoint` |
| **Extension SDK** | Done | `Developer/Sdk/ExtensionSdk.cs` |
| **Health Checks** | Done | `/healthz`, `/healthz/ready` |
| **Metrics Endpoint** | Done | `/metrics` (Prometheus) |
| **Distributed Tracing** | Done | OpenTelemetry ASP.NET + HTTP + source `AVICRM` |
| **Structured Logging** | Done | Serilog Compact JSON |
| **OpenTelemetry Integration** | Done | tracing + metrics + optional OTLP |

### EDD flow (MassTransit)

```
Domain event  →  IDomainEventPublisher  →  Integration event  →  Consumer
                      (MassTransit)           (bus message)      (metrics/log)
```

| `RabbitMq:HostName` | Transport |
|---------------------|-----------|
| empty | **InMemory** (default local) |
| set | **RabbitMQ** |

```csharp
await _events.PublishAsync(new PostPublishedDomainEvent(
    post.Id, post.Title, post.Slug, post.AuthorId, post.PublishedAtUtc!.Value));
```

Consumers: `PostPublished`, `PostCreated`, `CommentApproved`, `AuthorFollowed`.

### Endpoints

| Path | Notes |
|------|--------|
| `/healthz` | liveness |
| `/healthz/ready` | SQLite readiness |
| `/metrics` | Prometheus scrape |
| `/widgets/{zone}` | HTML widget zone |
| `/dev/bus` | SuperAdmin — transport status |
| `/dev/plugins` | SuperAdmin — loaded plugins |

### Build

```bash
cd AVICRM && dotnet restore && dotnet run
# solution has only AVICRM:
dotnet build AVICRM.sln
```
