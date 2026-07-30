# Social Features (FEATURES.md)

Implemented on branch `dev`:

| Feature | Status |
|--------|--------|
| **Likes** | `PostLike` + `Post.LikeCount` + `SocialController.ToggleLike` |
| **Reactions** | `PostReaction` (Like/Love/Clap/Insight/Funny) + `SocialController.React` |
| **Share buttons** | Copy / X / LinkedIn / Telegram in `_SocialBar` |
| **Social Login** | `ExternalLogin` + callback; enable with OAuth keys |
| **Follow Authors** | existing `FollowController.Toggle` + profile UI |
| **Follow Categories** | `FollowController.ToggleCategory` + bar button |
| **User Activity Feed** | `/Feed` — `FeedController` + `UserActivity` |
| **@Mentions** | `MentionsService` on comments → notify + activity |

## Wire-up required in your local tree

1. **Program.cs** — register:
```csharp
builder.Services.AddScoped<MentionsService>();
```

2. **PostsController.Details** — before `return View(post)`:
```csharp
await LoadTaxonomyContextAsync(post);
await TryLoadSocialContextAsync(post);
return View(post);
```

3. **AddComment** — after save (optional mentions):
```csharp
var mentions = HttpContext.RequestServices.GetService<MentionsService>();
var actorId = AuthorAccess.UserId(User) ?? "guest";
if (mentions != null && actorId != "guest")
    await mentions.ProcessCommentMentionsAsync(body, actorId, post.Id, comment.Id, post.Slug);
```

4. **OAuth packages** (optional):
```xml
<PackageReference Include="Microsoft.AspNetCore.Authentication.Google" Version="10.0.0" />
```
```csharp
if (!string.IsNullOrEmpty(builder.Configuration["Authentication:Google:ClientId"]))
{
    builder.Services.AddAuthentication()
        .AddGoogle(o => {
            o.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
            o.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
        });
}
```

5. **Nav**: link to `asp-controller="Feed" asp-action="Index"` for «فعالیت‌ها».

Restart the app once so `SchemaBootstrap.EnsureSocialTablesAsync` creates tables.
