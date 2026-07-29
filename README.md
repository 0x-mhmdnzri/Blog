# Dark Pro Blog

An ASP.NET Core MVC blog where every post is written README-style — Markdown in,
styled HTML out — with text, code, images, and video all stored in one SQLite
database. Public site and admin panel are RTL, Persian, and set in Vazirmatn.

## Stack

- **ASP.NET Core MVC (.NET 10 LTS)**
- **EF Core 10 + SQLite** — one `blog.db` holds posts, media bytes, comments
- **Markdig** — GitHub-flavored Markdown + `{{video:ID}}` embeds
- **Serilog** — structured Compact JSON for ELK (stdout + `/app/logs`)
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

All secrets and tuning live in **`.env`** — nothing sensitive is hardcoded in
`docker-compose.yml`. The image runs as non-root (`uid 1654`), with healthchecks,
CPU/memory limits, and Docker log rotation.

```bash
# logs (JSON lines on stdout)
docker compose logs -f blog

# backup SQLite
docker run --rm -v dark-pro-blog-data:/data -v "$PWD":/backup \
  alpine cp /data/blog.db /backup/blog-backup.db
```

Stop / remove containers (volumes kept):

```bash
docker compose down
```

## Local run (SDK 10)

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
cd BlogApp
dotnet restore
dotnet run
```

## Sign in

Defaults from `.env` / `appsettings.json`:

- User: `admin`
- Password: change `Admin__Password` before public deploy

Then open `/Account/Login` → `/Admin`.

## Configuration map

| Variable | Role |
| --- | --- |
| `Admin__Username` / `Admin__Password` | Seeded SuperAdmin |
| `Seo__BaseUrl` | Canonical / OG base URL |
| `ConnectionStrings__DefaultConnection` | SQLite path |
| `Serilog__MinimumLevel__*` | Log levels |
| `ForceHttps` | Only if container terminates TLS |
| `BLOG_HTTP_PORT` | Host port mapping |

## Project layout

```
Dockerfile, docker-compose.yml, .env.example, .dockerignore
BlogApp/   Controllers, Models, Data, Services, Middleware, Logging, Views, wwwroot
```
