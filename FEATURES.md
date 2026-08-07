# Remaining features (not yet fully implemented)

Items below are **not** fully done yet. Everything else is treated as shipped in the monolith (`BlogApp/`).

### User Experience

* ~~Reading Progress Indicator polish (edge cases on short posts)~~
* ~~Infinite Scroll reliability on slow networks~~
* ~~Table of Contents auto-highlight on deep posts~~

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

* ~~Publish post as campaign (one action)~~
* ~~Subscriber CSV import with double opt-in rules~~

### Accessibility

* Automated Accessibility Checker (beyond static checklist page)
* Screen Reader Optimization audit of public templates
* Keyboard Navigation pass on admin + public
* High Contrast Mode completeness (theme + reader)

### Developer Features

* Plugin sample DLL + docs for Extension SDK
* Output-cache invalidation consumer on domain events

### Enterprise Features

* ~~Multi-Tenant Support~~
* ~~Workspace Isolation~~
* ~~Custom Domains~~
* ~~SSO (OIDC/SAML)~~
* ~~Approval Workflow (formal states)~~
* ~~Content Lifecycle Management~~
* ~~Legal Hold~~
* ~~Data Export (GDPR download-my-data)~~
* ~~GDPR Compliance (erase account, consent logs)~~
* ~~Backup & Restore UI~~
* ~~Disaster Recovery runbooks / automate~~
* ~~Localization Management (editorial)~~

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

## PRD: Crawl Rate & Indexing Optimization

**Status:** In progress  
**Owner:** Mohammad Nazari  
**Last updated:** 2026-08-07  

Primary goal: sustained increase in crawl frequency, indexing rate, and domain authority — measured, not anecdotal.

### Success metrics

| Metric | Source | Target |
|--------|--------|--------|
| Pages crawled/day (by bot) | Server log analysis | Baseline Month 1, then trend |
| Crawl requests wasted on non-200/non-canonical | Log analysis | <5% of total |
| Time-to-index for new pages | GSC Coverage / URL Inspection | −30–50% |
| Indexed / total canonical pages | GSC Index Coverage | >90% |
| TTFB median | RUM / synthetic + bot log p50 | <200ms |
| Domain authority proxy | Ahrefs/Moz/Majestic | QoQ upward |

### P0 — Foundational

- [x] **P0.1** Bot log pipeline: capture user-agent + path + status + response time for Googlebot/Bingbot/AI bots (90-day retention, admin summary on SEO → Crawl) — *shipped 2026-08-07*
- [x] **P0.2** Server response under crawl load (TTFB <200ms median; hostload / stability) — *shipped 2026-08-07*
  - Skip analytics/cookies for known bots (unblocks OutputCache storage)
  - `[OutputCache(PolicyName="post")]` on post Details; home policy available
  - Skip schedule tick on bot hits (background worker owns publish/expire)
  - `Server-Timing: app;dur=` header for TTFB observability
  - Crawl tab: p50 / p95 / % hits >200ms
- [x] **P0.3** Explicit `robots.txt` policy for AI crawlers (allow high-value public content; disallow admin/account/api/private) — *shipped 2026-08-07*

### P1 — Crawl waste elimination

- [x] **P1.1** Redirect chains, soft 404s, duplicate/parameter URLs (canonicalize or noindex) — *shipped 2026-08-07*
  - `CanonicalUrlMiddleware`: strip utm/gclid/fbclid… + trailing slash → 301
  - `RedirectMiddleware`: flatten chains (max 5 hops) to one response
  - Unknown category/tag slug → hard 404 (no empty soft-404)
  - Bots skip culture cookie (OutputCache-friendly)
  - Crawl tab: waste report (chains, query hits, top 404s)
- [x] **P1.2** Orphan pages (zero internal inlinks): link in or remove/redirect — *shipped 2026-08-07*
  - `OrphanPageAnalyzer`: content inlinks from Markdown + hub exceptions (featured/sticky/series/folder)
  - SEO → Orphans tab: list + Feature (hub) or Redirect+unpublish (301)
  - Paths covered: `/{lang}/post/{slug}` and `/post/{slug}`
- [x] **P1.3** Clean XML sitemap(s): only canonical, indexable, 200 URLs + accurate `lastmod`; split by type if large — *shipped 2026-08-07*
  - `sitemap.xml` → sitemap index
  - Children: `sitemap-pages.xml`, `sitemap-posts.xml`, `sitemap-authors.xml`, `sitemap-taxonomies.xml`
  - Posts: published, not deleted/expired/scheduled-future; original/approved translations only
  - `lastmod` = max(UpdatedAt, PublishedAt) as W3C datetime
  - Taxonomies: only categories/tags/series with ≥1 live post; series at `/series/{slug}`
  - ResponseCache 30–60 min

### P2 — Discoverability / internal architecture

- [x] **P2.1** Priority pages ≤3–4 clicks from homepage — *shipped 2026-08-07*
  - `ClickDepthAnalyzer`: BFS depth via home feed, category/series hubs, related peers
  - SEO → Depth tab: histogram + beyond-4 + unreachable
  - Footer hubs (categories / series / authors) sitewide — 1 click from any page
  - Home series strip for discovery
- [ ] **P2.2** New posts linked from high-authority hubs on publish (not sitemap-only)

### P3 — Freshness & demand signals

- [ ] **P3.1** Content update cadence for pages we want recrawled often
- [ ] **P3.2** Structured data + mobile rendering parity (Googlebot Smartphone)

### P4 — Authority (ongoing)

- [ ] **P4.1** Quality backlink acquisition
- [ ] **P4.2** Quarterly DA/DR review (not weekly noise)

### Ongoing discipline

- [ ] Monthly crawl-health audit (logs + GSC Coverage)
- [ ] Crawl budget dashboard (logs + GSC + server performance)

### Notes

- Log analysis is ground truth; GSC alone is not enough.
- AI crawler blocking is a product tradeoff (search budget vs AI-answer surfaces) — policy is explicit, not default-deny-all.
- Full PRD body lives in product docs / this checklist drives implementation order.

---

## Implemented (reference)

Markdown editor, soft delete, reading-time field, SEO metadata / canonical / sitemap / robots / OG / JSON-LD / slugs / redirects / broken links, nested categories & tags & series, media library & upload, threaded comments & moderation & reactions & reporting, search (FTS5 full-content / Spotlight-style) & bookmarks & reading history & dark mode, likes & reactions & share & follow authors & activity feed & @mentions, email + in-app notifications & digests, post view / traffic / geo / device / referral / popular / trending / heatmaps analytics, search keyword analytics & reading duration & bounce rate, account lockout & audit logs & rate limiting & sessions, user management & feature flags & site settings & maintenance & reports, REST API & API keys & webhooks & rate limits & RSS/Atom, response caching helpers & background jobs & search index worker, language switcher & RTL, memberships/premium/donations/sponsored labels (basic), newsletter subscribe / campaigns / segments / double opt-in / schedule / **CSV import (double opt-in)** / **post→campaign one action** / export subscribers, theme system & widgets & middleware slots & extension SDK surface & health & metrics & tracing & structured logging & OpenTelemetry & MassTransit domain events, dismissible site announcement banner, unified moderation queue (comments + reports + spam), output cache (home/post/taxonomy) with publish invalidation, Redis distributed cache in compose, background job dead-letter UI with email retries, **reading progress bar (short-post safe)**, **infinite scroll with retry/timeout**, **TOC IntersectionObserver auto-highlight on deep posts**, **enterprise module** (tenants, workspaces, domains, SSO config, approval, lifecycle, legal hold, GDPR export/erase, backup/DR, localization admin), **robots.txt AI crawler policy (P0.3)**, **bot crawl log pipeline (P0.1)**, **crawl TTFB/hostload stability (P0.2)**, **crawl waste reduction (P1.1)**, **orphan pages (P1.2)**, **clean split sitemaps (P1.3)**, **click-depth ≤4 (P2.1)**.
