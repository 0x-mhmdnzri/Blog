# Remaining features (not yet fully implemented)

Items already shipped in `BlogApp` were removed from this list.
Last reviewed against the monolith codebase on 2026-07-31.

---

### User Experience

* Full-Text Search (SQLite FTS5 or equivalent)
* Search Suggestions (typeahead)
* Advanced Search Filters (date, tag, author, language)
* Infinite Scrolling (optional)
* Reading Progress Bar (verify on post page)
* Font Size Preferences (persist reader preference)

### Social Features

*(shipped — see `SOCIAL.md`: OAuth Google/GitHub with production hardening, follow categories on Home + post bar, likes/reactions/share/follow authors/activity feed/@mentions)*

### Notifications

* Push Notifications (real sender; replace NoOp)
* Post → newsletter one-click send

### Analytics

* Search Keyword Analytics
* Average Reading Duration (scroll/time tracking)
* Bounce Rate Tracking

### Security

* Two-Factor Authentication (TOTP)
* Email Verification flow (register → confirm link)
* CAPTCHA Integration (login / register / comment / subscribe)
* Content Approval Workflow (author submit → admin publish)

### Administration

* Announcement Banner (site-wide dismissible)
* Moderation Queue polish (spam + reports in one inbox)

### API

* Real GraphQL engine (current `/api/graphql` is a minimal custom parser)
* OpenAPI / Swagger documentation page completeness

### Performance

* Output Caching (full HTML for home / post / taxonomy; invalidate on publish)
* Redis as default distributed cache in production compose
* Queue-based Email Delivery reliability (retries, dead-letter UI)

### Internationalization

* Multi-language Posts product UX (link translations, switcher on post)
* Localized URLs consistency (`/{lang}/post/{slug}` everywhere)
* Translation Workflow (status, assignees, review)

### Monetization

* Stripe (or provider) Checkout for memberships
* Advertisement Management
* Affiliate Link Management
* Member-only Markdown blocks (`:::members`)

### Newsletter

* Publish post as campaign (one action)
* Subscriber CSV import with double opt-in rules

### Accessibility

* Automated Accessibility Checker (beyond static checklist page)
* Screen Reader Optimization audit of public templates
* Keyboard Navigation pass on admin + public
* High Contrast Mode completeness (theme + reader)

### Developer Features

* Plugin sample DLL + docs for Extension SDK
* Output-cache invalidation consumer on domain events

### Enterprise Features

* Multi-Tenant Support
* Workspace Isolation
* Custom Domains
* SSO (OIDC/SAML)
* Approval Workflow (formal states)
* Content Lifecycle Management
* Legal Hold
* Data Export (GDPR download-my-data)
* GDPR Compliance (erase account, consent logs)
* Backup & Restore UI
* Disaster Recovery runbooks / automate
* Localization Management (editorial)

### AI Features

* Semantic Search
* AI Recommendations
* Similar Articles
* Automatic Tag Generation (LLM)
* Automatic Category Classification
* AI-generated Cover Images
* Duplicate Content Detection
* Content Quality Score
* AI-powered Comment Moderation
* Personalized Content Feed

---

### Implemented (removed from active backlog)

**Comments** (rule-based spam detection with score/reasons + `CommentStatus.Spam`; guest comments with honeypot, optional email, IP hash, rate limit policy `comment`; pinned comments for post authors/admins; author edit window via `Comments:EditWindowMinutes`; Admin → Comments spam tab + pin/mark-spam actions; merged into existing Admin Comments — no new sidebar).

**Media** (ImageSharp WebP/JPEG re-encode + max-width resize + EXIF strip via background job; responsive variants at configurable widths served from `/media/{id}/w/{width}`; `srcset`/`sizes` injected by MarkdownService; `MediaVersions` history + restore; CDN base URL rewrite on upload JSON, markdown images, and video embeds; Admin → Media re-optimize + versions API).

**SEO** (WordPress WXR + Ghost JSON migration importer with auto-301 redirects in Admin → SEO Tools → Import; IndexNow + Bing ping on `post.published` via MassTransit consumer + manual submit-all; key verification at `/{key}.txt`; hreflang alternate links on post pages from translation groups + sitemap `xhtml:link` alternates + x-default).

**Content Management** (drafts & continuous autosave local+server, post revisions browse/diff/restore, scheduled publish + expiration always-on hosted worker, post duplication, featured/sticky ordering on home feeds, rich-text editor toggle, sticky TOC navigation, AI summarize/grammar/assist with optional OpenAI-compatible LLM fallback).

Markdown editor, soft delete, reading-time field, SEO metadata / canonical / sitemap / robots / OG / JSON-LD / slugs / redirects / broken links, nested categories & tags & series, media library & upload, threaded comments & moderation & reactions & reporting, search (basic) & bookmarks & reading history & dark mode, likes & reactions & share & follow authors & activity feed & @mentions, email + in-app notifications & digests, post view / traffic / geo / device / referral / popular / trending / heatmaps analytics, account lockout & audit logs & rate limiting & sessions, user management & feature flags & site settings & maintenance & reports, REST API & API keys & webhooks & rate limits & RSS/Atom, response caching helpers & background jobs & search index worker, language switcher & RTL, memberships/premium/donations/sponsored labels (basic), newsletter subscribe / campaigns / segments / double opt-in / schedule, theme system & widgets & middleware slots & extension SDK surface & health & metrics & tracing & structured logging & OpenTelemetry & MassTransit domain events.
