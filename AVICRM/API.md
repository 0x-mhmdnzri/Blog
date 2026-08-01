# API (FEATURES.md)

| Item | Path / notes |
|------|----------------|
| REST API | `/api/v1/posts`, `/api/v1/comments`, `/api/v1/webhooks` |
| GraphQL API | `POST /api/graphql` — `posts` / `post(slug)` |
| API Keys (PAT) | User: `/AccountApiKeys` · SuperAdmin: `/AdminApiKeys` |
| Webhooks | CRUD under `/api/v1/webhooks` (https only, SSRF blocked) |
| API Rate Limiting | Policy `api` (60/min per key); 429 → abuse strike |
| Auto-ban | 5 rate-limit strikes → key banned |
| API Documentation | `GET /api/docs` |
| Public RSS | `/feed/rss` |
| Atom Feed | `/feed/atom` |
| FluentValidation | All write DTOs + InputSanitizer |

## Auth

```
X-Api-Key: blog_<hex>
Authorization: Bearer blog_<hex>
```

Scopes: `read`, `write`, `webhooks`

## Create a key

1. Register / login
2. Open **کلیدهای API** (`/AccountApiKeys`)
3. Create PAT (shown once)
4. SuperAdmin moderates at `/AdminApiKeys` (ban / disable / delete)
