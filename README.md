# Dark Pro Blog

**Architecture: single ASP.NET Core web app monolith — only `BlogApp/`.**  
No class libraries, no n-tier / Clean Architecture project split. One process, one `.csproj`.

An ASP.NET Core MVC blog where every post is written README-style — Markdown in,
styled HTML out — with text, code, images, and video in one SQLite database.
Public site and admin panel are RTL, Persian, and set in Vazirmatn.

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
- **API** — FluentValidation, topic-bus request handling, per-key rate visibility
- **Perf** — compiled queries, NoTracking default, connection pool, ReadyToRun

See [FEATURES.md](FEATURES.md), [DEVELOPER.md](DEVELOPER.md), [SOCIAL.md](SOCIAL.md), [BlogApp/API.md](BlogApp/API.md).

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
| --- | --- | --- |
| `dark-pro-blog-data` | `/app/data` | SQLite + CMS state |
| `dark-pro-blog-logs` | `/app/logs` | Rolling Serilog JSON |

```bash
docker compose logs -f blog
docker compose down          # keep volumes
docker compose down -v       # wipe data + logs
```

### RabbitMQ (optional)

Default is **in-process** MassTransit (no broker). To use RabbitMQ:

```bash
# 1) start broker
docker compose --profile rabbitmq up -d

# 2) in .env set:
#    RabbitMq__HostName=rabbitmq
#    RabbitMq__UserName=blog
#    RabbitMq__Password=…   # must match RABBITMQ_PASS

# 3) recreate app so it picks up the host
docker compose up -d --force-recreate blog
```

Management UI: http://localhost:15672 (user/pass from `.env`).

### How env works

1. **`docker compose`** loads project-root `.env` for *interpolation* (`${BLOG_HTTP_PORT}`, image names, resource limits).
2. **`env_file: .env`** injects the same file into the **container environment**.
3. ASP.NET Core maps `Section__Key=value` → configuration `Section:Key` (overrides `appsettings*.json`).

Do **not** put application secrets in `docker-compose.yml`. Edit `.env` only.

## Local run (SDK 10)

```bash
cp .env.example .env          # optional for local; or edit BlogApp/appsettings.json
cd BlogApp
dotnet restore
dotnet run
```

Solution contains **only** `BlogApp`:

```bash
dotnet build BlogApp.sln
```

## Sign in

- Username: `Admin__Username` (default `admin`)
- Password: `Admin__Password` from `.env` / `appsettings.json`

Then `/Account/Login` → `/Admin`. Change the password before any public deploy.

## Project layout

```
BlogApp.sln
Dockerfile
docker-compose.yml
.env.example                 # cp → .env (gitignored)
FEATURES.md  DEVELOPER.md  SOCIAL.md
BlogApp/                     # entire application
  Controllers/  Data/  Models/  Services/  Views/  wwwroot/
  Api/                       # HTTP API + FluentValidation + PAT keys
  Developer/                 # MassTransit consumers, health, metrics, plugins
  plugins/  themes/
```

There is **no** multi-project Domain / Application / Infrastructure tree in the build.

## Production checklist

1. `cp .env.example .env` and set strong `Admin__Password`
2. Set `Seo__BaseUrl` to the public HTTPS origin
3. Put TLS on a reverse proxy; keep `ForceHttps=false` unless the app terminates TLS
4. Persist volumes `blog_data` / `blog_logs` (or bind mounts)
5. Optional: enable RabbitMQ profile + `RabbitMq__HostName`
6. Optional: fill `Authentication__Google__*` / `Authentication__GitHub__*` for social login
