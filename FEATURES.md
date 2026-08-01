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

* ~~Push Notifications (real sender; replace NoOp)~~ — WebPushSender + PushSubscriptions + SSE bell/inbox
* ~~Post → newsletter one-click send~~ — Posts/SendToNewsletter
* Email-style inbox (folders, star, archive, search, live SSE) — shipped
* MassTransit consumers + RabbitMQ webhook fan-out — shipped

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
