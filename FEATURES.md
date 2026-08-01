# Remaining features (not yet fully implemented)

Items below are **not** fully done yet. Everything else is treated as shipped in the monolith (`BlogApp/`).

### User Experience

* Reading Progress Indicator polish (edge cases on short posts)
* Infinite Scroll reliability on slow networks
* Table of Contents auto-highlight on deep posts

### Analytics

* ~~Search Keyword Analytics~~
* ~~Average Reading Duration (scroll/time tracking)~~
* ~~Bounce Rate Tracking~~

### Security

* Two-Factor Authentication (TOTP)
* Email Verification flow (register → confirm link)
* CAPTCHA Integration (login / register / comment / subscribe)
* Content Approval Workflow (author submit → admin publish)

### Administration

* ~~Announcement Banner (site-wide dismissible)~~
* ~~Moderation Queue polish (spam + reports in one inbox)~~

### API

* Real GraphQL engine (current `/api/graphql` is a minimal custom parser)
* OpenAPI / Swagger documentation page completeness

### Performance

* ~~Output Caching (full HTML for home / post / taxonomy; invalidate on publish)~~
* ~~Redis as default distributed cache in production compose~~
* ~~Queue-based Email Delivery reliability (retries, dead-letter UI)~~

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
* Content Summarization
* Auto-translate post drafts

---

## Implemented (reference)

Markdown editor, soft delete, reading-time field, SEO metadata / canonical / sitemap / robots / OG / JSON-LD / slugs / redirects / broken links, nested categories & tags & series, media library & upload, threaded comments & moderation & reactions & reporting, search (FTS5 full-content / Spotlight-style) & bookmarks & reading history & dark mode, likes & reactions & share & follow authors & activity feed & @mentions, email + in-app notifications & digests, post view / traffic / geo / device / referral / popular / trending / heatmaps analytics, search keyword analytics & reading duration & bounce rate, account lockout & audit logs & rate limiting & sessions, user management & feature flags & site settings & maintenance & reports, REST API & API keys & webhooks & rate limits & RSS/Atom, response caching helpers & background jobs & search index worker, language switcher & RTL, memberships/premium/donations/sponsored labels (basic), newsletter subscribe / campaigns / segments / double opt-in / schedule, theme system & widgets & middleware slots & extension SDK surface & health & metrics & tracing & structured logging & OpenTelemetry & MassTransit domain events, dismissible site announcement banner, unified moderation queue (comments + reports + spam), output cache (home/post/taxonomy) with publish invalidation, Redis distributed cache in compose, background job dead-letter UI with email retries.
