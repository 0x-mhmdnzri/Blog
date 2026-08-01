# AVICRM

**Architecture: single ASP.NET Core web app monolith — only `BlogApp/`.**  
No class libraries, no n-tier / Clean Architecture project split. One process, one `.csproj`.

**AVICRM** — ASP.NET Core application evolving from a Markdown publishing core toward a full CRM (see [FEATURES.md](FEATURES.md)). Current codebase remains the monolith under `BlogApp/`. Publishing surface: Markdown in, styled HTML out — text, code, images, and video in one SQLite database. Public site and admin panel support RTL (e.g. Persian) and Vazirmatn.

## Stack

| Layer | Choice |
| --- | --- |
| Host | ASP.NET Core MVC (.NET 10 LTS) — `BlogApp` |
| Data | EF Core 10 + SQLite (`blog.db`) + FTS5 |
| Events | MassTransit (InMemory default, RabbitMQ optional) |
| Markdown | Markdig (GFM + `{{video:ID}}` embeds) |
| Logging | Serilog Compact JSON (stdout + `/app/logs`) |
| Observability | OpenTelemetry metrics (`/metrics`) + optional OTLP |
| UI | Bootstrap 5 RTL, Dark Pro theme, Vazirmatn |
| SEO | Open Graph, JSON-LD, sitemap, robots, IndexNow |
| Auth | Cookie + optional Google / GitHub OAuth |
| Realtime | SSE notifications (`/Notifications/Stream`, `/Admin/Stream`) |

## Features (high level)

- **Posts** — Markdown editor, autosave, media embeds, scheduled publish
- **Comments** — Twitter/X-style threads, likes, pin, spam scoring, admin moderation
- **Media** — upload, ImageSharp optimize, blur preload, HSL-friendly video
- **Taxonomy** — categories & tags, follow category
- **Social** — author follow, profile images, OAuth login
- **Admin** — DataTables, analytics, SEO tools, monetization, API keys (PAT)
- **Enterprise** — multi-tenant console, SSO config, legal hold, GDPR, backup
- **API** — FluentValidation, topic-bus request handling, per-key rate visibility
- **Perf** — compiled queries, NoTracking default, connection pool, ReadyToRun

CRM roadmap (Contacts, pipeline, automation, …): **[FEATURES.md](FEATURES.md)** — not all modules implemented yet.

See also [DEVELOPER.md](DEVELOPER.md), [SOCIAL.md](SOCIAL.md), [BlogApp/API.md](BlogApp/API.md).

## Docker (recommended)

Configuration is **only** via `.env` (Compose does not hardcode app secrets).

```bash
cp .env.example .env
# Required: change Admin__Password
# Recommended: set Seo__BaseUrl to your public URL
docker compose up --build -d
```

Open **http://localhost:8080** (or whatever `BLOG_HTTP_PORT` is in `.env`).

| Volume | Container path | Purpose |
| --- | --- |
| data volume | `/app/data` | SQLite + CMS state + backups |
| logs volume | `/app/logs` | Rolling Serilog JSON |

```bash
docker compose logs -f blog
docker compose down          # keep volumes
docker compose down -v       # wipe data + logs
```

### RabbitMQ (optional)

Default is **in-process** MassTransit (no broker). To use RabbitMQ, enable the compose profile and set `RabbitMq__HostName` in `.env`.

## Backup & recovery

SuperAdmin: **Admin → Backup & storage** (full ZIP download, volume gauges, process I/O).  
Policy: `Backup__IntervalHours` (RPO), `Backup__RetentionDays`, path on data volume (Docker: `/app/data/backups`; local dev: `App_Data/backups`).

## License

See repository license file if present.
