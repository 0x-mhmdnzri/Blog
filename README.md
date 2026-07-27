# Dark Pro Blog

An ASP.NET Core MVC blog where every post is written README-style — Markdown in,
styled HTML out — with text, code, images, and video all stored in the database.
Styled with Bootstrap 5 + a custom "Dark Pro" minimal theme.

## Stack

- **ASP.NET Core MVC (.NET 8)**
- **EF Core + SQLite** — one `blog.db` file holds posts, categories, tags, comments,
  and every uploaded image/video/file (as raw bytes). Nothing is written to disk.
- **Markdig** — GitHub-flavored Markdown rendering (tables, fenced code, task lists,
  footnotes, auto-links, emoji) plus a custom `{{video:ID}}` token for inline video.
- **Bootstrap 5** (dark theme) + a hand-built "Dark Pro" stylesheet
  (`wwwroot/css/dark-theme.css`).
- **highlight.js** for code syntax highlighting in the reading view.
- Cookie authentication — a single admin account (configured in `appsettings.json`)
  can create, edit, and delete posts; everyone else can read and comment.

## Getting started

```bash
cd BlogApp
dotnet restore
dotnet run
```

The app creates `blog.db` automatically on first run and seeds one welcome post —
no `dotnet ef` step required to get going.

Open **https://localhost:5001** (or the URL printed in the console).

### Sign in as the author

Default credentials are in `BlogApp/appsettings.json`:

```json
"Admin": { "Username": "admin", "Password": "ChangeMe123!" }
```

**Change this before deploying anywhere public.** For production, move these into
[user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) or
environment variables instead of the checked-in file, and put real hashed-password
auth behind it if more than one person will ever publish.

Visit `/Account/Login`, sign in, then use **New post** in the nav.

## Writing a post

The editor (`/Posts/Create`) is a plain textarea + live preview, no rich-text
limitations — you write Markdown exactly like a GitHub README:

```markdown
## A heading

Regular paragraph text, **bold**, _italic_, `inline code`.

```csharp
public record Post(string Title, string ContentMarkdown);
```

> Block quotes render as a highlighted callout.

| Feature | Supported |
| --- | --- |
| Tables | ✅ |
| Task lists | ✅ |
```

- **Images**: drop a file on the upload box (or click it) — it uploads to
  `/media/upload`, gets stored as bytes in the `MediaAssets` table, and an
  `![alt](/media/{id})` snippet is inserted at your cursor automatically.
- **Video**: same drop zone — video files get a `{{video:123}}` token instead,
  which the renderer expands into an HTML5 `<video>` player streamed straight
  from the database (with range-request support, so scrubbing works).
- **Any other file**: uploads the same way and becomes a plain download link.
- There's no length cap on `ContentMarkdown` — it's a `TEXT` column — so a post
  can be as long as you want.

## Project layout

```
BlogApp/
  Controllers/     HomeController (listing), PostsController (CRUD + reading),
                   MediaController (upload/stream), AccountController (login)
  Models/          Post, Category, Tag, PostTag, Comment, MediaAsset
  Data/            ApplicationDbContext, DbSeeder
  Services/        MarkdownService (Markdig pipeline), SlugHelper
  Views/           Razor views, dark-theme layout in Views/Shared/_Layout.cshtml
  wwwroot/css/     dark-theme.css — the whole visual identity in one file
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

## Switching to SQL Server instead of SQLite

Swap the `Microsoft.EntityFrameworkCore.Sqlite` package for
`Microsoft.EntityFrameworkCore.SqlServer`, change `UseSqlite` to `UseSqlServer` in
`Program.cs`, and update the connection string in `appsettings.json`.
