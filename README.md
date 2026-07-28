# Dark Pro Blog

An ASP.NET Core MVC blog where every post is written README-style — Markdown in,
styled HTML out — with text, code, images, and video all stored in one SQLite
database. Public site and admin panel are RTL, Persian, and set in Vazirmatn;
the admin panel has its own sidebar with a dashboard, post management, and a
comment moderation queue.

## Stack

- **ASP.NET Core MVC (.NET 8)**
- **EF Core + SQLite** — one `blog.db` file holds posts, categories, tags,
  comments, and every uploaded image/video/file (as raw bytes). Nothing is
  written to disk outside that one file.
- **Markdig** — GitHub-flavored Markdown rendering (tables, fenced code, task
  lists, footnotes, auto-links, emoji) plus a custom `{{video:ID}}` token for
  inline video.
- **Bootstrap 5 (RTL build)** + a hand-built "Dark Pro" stylesheet
  (`wwwroot/css/dark-theme.css`, `wwwroot/css/admin-rtl.css`).
- **highlight.js** for code syntax highlighting in the reading view — code
  blocks always render left-to-right regardless of the surrounding RTL text.
- **SeoService** — meta description, canonical URLs, Open Graph/Twitter Card
  tags, and JSON-LD (`BlogPosting`, `BreadcrumbList`, `Blog`/`WebSite`) for
  search and answer engines, plus a live `/sitemap.xml` and `/robots.txt`.
- Cookie authentication — a single admin account (configured via `appsettings.json`
  or environment variables) can create/edit/delete posts and moderate comments;
  everyone else can read and comment.

## Getting started (without Docker)

```bash
cd BlogApp
dotnet restore
dotnet run
```

The app creates `blog.db` automatically on first run and seeds one welcome
post — no `dotnet ef` step required to get going. Open the URL printed in the
console (defaults to `https://localhost:5001`).

## Getting started (Docker — fully standalone)

Everything the blog needs — posts, comments, and every uploaded image/video —
lives in a single SQLite file, so the whole app runs as one container with one
volume for that file. No separate database container, no separate file/media
storage to wire up.

```bash
docker compose up --build
```

Then open **http://localhost:8080**. That's the entire setup — `docker-compose.yml`
builds the image from the included `Dockerfile`, starts the container, and
mounts a named volume (`blog_data`) at `/app/data` so `blog.db` (and everything
in it) survives `docker compose down` / restarts / image upgrades.

To run it without Compose:

```bash
docker build -t dark-pro-blog .
docker run -d --name dark-pro-blog \
  -p 8080:8080 \
  -v dark_pro_blog_data:/app/data \
  -e Admin__Username=admin \
  -e Admin__Password=ChangeMe123! \
  -e Seo__BaseUrl=http://localhost:8080 \
  dark-pro-blog
```

**Before exposing the container publicly**, change these in `docker-compose.yml`
(or pass them as `-e` flags / a `.env` file):

| Variable | Purpose |
| --- | --- |
| `Admin__Username`, `Admin__Password` | Admin panel login |
| `Seo__SiteName`, `Seo__SiteDescription`, `Seo__AuthorName` | Meta tags & JSON-LD |
| `Seo__BaseUrl` | Your real public URL (used for canonical/OG tags) |
| `ConnectionStrings__DefaultConnection` | Only needed if you move the SQLite file elsewhere |

**TLS**: the container serves plain HTTP on port 8080 and expects a reverse
proxy (nginx, Traefik, Caddy, a cloud load balancer) in front of it to
terminate HTTPS — that's why `ForceHttps=false` is set in the image. If you're
hitting the container directly without a proxy, keep it that way; if you put a
proxy in front, point it at `http://<container>:8080` and terminate TLS there.

**Backing up your data**: the SQLite file is the entire database. To back it up:

```bash
docker run --rm -v dark-pro-blog_blog_data:/data -v "$PWD":/backup \
  alpine cp /data/blog.db /backup/blog-backup.db
```

(Volume name may differ — check with `docker volume ls`.)

### Sign in as the author

Default credentials (change via the environment variables above, or in
`BlogApp/appsettings.json` for non-Docker runs):

```json
"Admin": { "Username": "admin", "Password": "ChangeMe123!" }
```

Visit `/Account/Login`, sign in, then use **پنل مدیریت** (Admin panel) in the
nav — or go straight to `/Admin`.

## Admin panel

`/Admin` — RTL, Persian, Vazirmatn, with a sidebar:

- **داشبورد** (Dashboard) — post/comment/media counts, recent comments
- **نوشته‌ها** (Posts) — every post with publish/unpublish toggle, edit, view/comment counts
- **دیدگاه‌ها** (Comments) — moderation queue: در انتظار / تأییدشده / ردشده / همه
  (pending / approved / rejected / all), with approve/reject/delete actions
- **رسانه‌ها**, **دسته‌بندی‌ها**, **تنظیمات** — demo placeholders (tagged "دمو")
  for planned features

## Writing a post

The editor (inside the admin panel) is a plain textarea + live preview, no
rich-text limitations — you write Markdown exactly like a GitHub README, with
headings, bold/italic, inline code, fenced code blocks, block quotes, and
tables all supported.

- **Images**: drop a file on the upload box (or click it) — it uploads to
  `/media/upload`, gets stored as bytes in the `MediaAssets` table, and an
  `![alt](/media/{id})` snippet is inserted at your cursor automatically.
- **Video**: same drop zone — video files get a `{{video:123}}` token instead,
  which the renderer expands into an HTML5 `<video>` player streamed straight
  from the database (with range-request support, so scrubbing works).
- **Any other file**: uploads the same way and becomes a plain download link.
- There's no length cap on `ContentMarkdown` — it's a `TEXT` column — so a post
  can be as long as you want.
- Code blocks always render left-to-right, even when the rest of the post is
  Persian — that's a hard CSS rule, not something you need to manage per post.

## Project layout

```
Dockerfile, docker-compose.yml, .dockerignore   — standalone container setup
BlogApp/
  Controllers/     Home (listing), Posts (CRUD + reading), Admin (dashboard,
                    post management, comment moderation), Media (upload/stream),
                    Account (login), Seo (robots.txt, sitemap.xml)
  Models/          Post, Category, Tag, PostTag, Comment (+ CommentStatus), MediaAsset
  Data/            ApplicationDbContext, DbSeeder
  Services/        MarkdownService (Markdig pipeline), SlugHelper, SeoService
  Views/           Public site views + Views/Admin (RTL sidebar layout)
  wwwroot/css/     dark-theme.css (site-wide) + admin-rtl.css (admin-specific)
  wwwroot/js/      editor.js — toolbar, live preview, drag-and-drop upload
```

## Switching to full EF Core migrations

The app uses `EnsureCreated()` for a zero-friction first run. Once you're ready
for real migrations (recommended before you start changing the schema):

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

Then in `Program.cs`, swap `db.Database.EnsureCreated()` for `db.Database.Migrate()`.
If you do this, also add a migrations-on-startup step to the Dockerfile/entrypoint
so a fresh container applies them automatically.

## Switching to SQL Server instead of SQLite

Swap the `Microsoft.EntityFrameworkCore.Sqlite` package for
`Microsoft.EntityFrameworkCore.SqlServer`, change `UseSqlite` to `UseSqlServer` in
`Program.cs`, update the connection string, and add a `sqlserver` service to
`docker-compose.yml` (at that point media stays in the DB either way — you're
just changing which database engine hosts it).
