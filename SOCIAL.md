# Social Features

Production-ready social layer for Dark Pro Blog (branch `dev`).

## Feature matrix

| Feature | Surface | Notes |
|--------|---------|--------|
| **Likes** | `_SocialBar` + `SocialController.ToggleLike` | Binary heart; updates `Post.LikeCount` |
| **Reactions** | `_SocialBar` + `SocialController.React` | Like / Love / Clap / Insight / Funny |
| **Share** | Copy, X, LinkedIn, Telegram | Client-side + intent URLs |
| **Follow authors** | Profile + post bar + `FollowController.Toggle` | Activity + notification |
| **Follow categories** | Post bar + **Home category banner** + `ToggleCategory` | Feeds activity stream |
| **Activity feed** | `/Feed` (`FeedController`) | Authors you follow + categories + mentions |
| **@Mentions** | Comments → `MentionsService` | In-app notification + activity |
| **Threaded comments** | `_CommentsSection` | Twitter-style replies, likes, pin, edit window, spam |
| **Social Login** | Google + GitHub | Optional; keys via config/env |

## Social login (Google / GitHub)

### 1. Create OAuth apps

**Google Cloud Console**

1. APIs & Services → Credentials → Create OAuth client (Web)
2. Authorized redirect URI: `{Seo:BaseUrl}/signin-google`
3. Copy Client ID + Client Secret

**GitHub OAuth Apps**

1. Homepage URL: `{Seo:BaseUrl}`
2. Authorization callback URL: `{Seo:BaseUrl}/signin-github`
3. Copy Client ID + Client Secret

### 2. Configure environment

```bash
Authentication__Google__ClientId=
Authentication__Google__ClientSecret=
Authentication__GitHub__ClientId=
Authentication__GitHub__ClientSecret=
Seo__BaseUrl=https://blog.example.com
```

Empty ClientId or ClientSecret → provider disabled (buttons hidden).

### 3. Production hardening

- Schemes register only when both ClientId and ClientSecret are present
- ExternalLogin rejects unknown providers
- Correlation cookies: HttpOnly, SameSite=Lax
- Auth cookie SameSite=Lax when OAuth enabled, else Strict
- New external users auto-provisioned as Reader
- Rate limit policy login on ExternalLogin POST

### 4. Packages

```xml
<PackageReference Include="Microsoft.AspNetCore.Authentication.Google" Version="10.0.9" />
<PackageReference Include="AspNet.Security.OAuth.GitHub" Version="9.3.0" />
```

## Follow categories UX

1. Home `?category={slug}` → banner with follow button
2. Post `_SocialBar` also toggles category follow
3. Activity on `/Feed`

## Comments (Twitter/X style)

Nested replies, inline composer, relative Persian time, avatars, like/pin/edit, spam moderation under Admin → Comments.
