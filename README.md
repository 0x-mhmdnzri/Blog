# Dark Pro Blog

**Architecture: single ASP.NET Core web app monolith — only `BlogApp/`.**  
No class libraries, no n-tier / Clean Architecture project split. Everything runs in one process and one `.csproj`.

An ASP.NET Core MVC blog where every post is written README-style — Markdown in,
styled HTML out — with text, code, images, and video all stored in one SQLite
database. Public site and admin panel are RTL, Persian, and set in Vazirmatn.

## Stack

- **ASP.NET Core MVC (.NET 10 LTS)** — one host project: `BlogApp`
- **EF Core 10 + SQLite** — one `blog.db` holds posts, media bytes, comments
- **MassTransit** — domain/integration events (InMemory or RabbitMQ)
- **Markdig** — GitHub-flavored Markdown + `{{video:ID}}` embeds
- **Serilog** — structured Compact JSON for ELK (stdout + `/app/logs`)
- **OpenTelemetry** — metrics (`/metrics`) + optional OTLP tracing
- **Bootstrap 5 RTL** + Dark Pro theme, Vazirmatn
- **SeoService** — Open Graph, JSON-LD, sitemap, robots, redirects

## Docker (recommended)

```bash
cp .env.example .env
# edit Admin__Password, Seo__BaseUrl, etc.
docker compose up --build -d
```

Open **http://localhost:8080** (or `BLOG_HTTP_PORT` from `.env`).

| Volume | Path | Purpose |
| --- | --- | --- |
| `dark-pro-blog-data` | `/app/data` | SQLite + all CMS state |
| `dark-pro-blog-logs` | `/app/logs` | Rolling Serilog JSON |

```bash
docker compose logs -f blog
docker compose down
```

## Local run (SDK 10)

```bash
cd BlogApp
dotnet restore
dotnet run
```

Solution file contains **only** `BlogApp`:

```bash
dotnet build BlogApp.sln
```

## Sign in

- User: `admin` (from `.env` / `appsettings.json`)
- Password: change `Admin__Password` before public deploy

Then open `/Account/Login` → `/Admin`.

## Project layout

```
BlogApp.sln                 # single project
Dockerfile, docker-compose.yml, .env.example
BlogApp/                    # THE entire application
  Controllers/
  Data/
  Developer/                # MassTransit EDD, health, metrics, plugins, widgets
  Logging/
  Middleware/
  Models/
  Services/
  Views/
  wwwroot/
  plugins/                  # optional drop-in DLLs
  themes/                   # .blogtheme packs
FEATURES.md
DEVELOPER.md
```

There is **no** `src/` Domain / Application / Infrastructure multi-project tree in the build.
