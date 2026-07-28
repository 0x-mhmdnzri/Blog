using BlogApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db)
    {
        if (!await db.Categories.AnyAsync())
        {
            db.Categories.AddRange(
                new Category { Name = "دات‌نت", Slug = "dotnet" },
                new Category { Name = "معماری", Slug = "architecture" },
                new Category { Name = "یادداشت‌ها", Slug = "notes" }
            );
        }

        if (!await db.Posts.AnyAsync())
        {
            var dotnet = await db.Categories.FirstOrDefaultAsync(c => c.Slug == "dotnet");

            db.Posts.Add(new Post
            {
                Title = "به وبلاگ خوش آمدید",
                Slug = "welcome-to-the-blog",
                Summary = "این وبلاگ چطور کار می‌کند: متن، تصویر، ویدیو و کد — همه در یک پایگاه‌داده ذخیره می‌شوند.",
                Category = dotnet,
                IsPublished = true,
                PublishedAtUtc = DateTime.UtcNow,
                ContentMarkdown =
"""
# خوش آمدید

این ویرایشگر به سبک **ریدمی** کار می‌کند. مارک‌داون ساده بنویسید و دقیقاً مثل یک
ریدمی گیت‌هاب نمایش داده می‌شود — تیتر، لیست، جدول، نقل‌قول، و بلوک کد:

```csharp
public record Post(string Title, string Slug, string ContentMarkdown);
```

توجه کنید که بلوک کد بالا همیشه چپ‌به‌راست باقی می‌ماند، حتی هنگامی که
بقیه متن راست‌به‌چپ و فارسی است — این دقیقاً همان رفتاری است که یک بلوک
کد باید داشته باشد.

> هر تصویر، ویدیو یا فایلی که در یک نوشته قرار می‌دهید مستقیماً به‌صورت
> بایت در پایگاه‌داده ذخیره می‌شود — چیزی روی دیسک نوشته نمی‌شود. با حذف
> نوشته، رسانه‌های آن هم به‌طور خودکار حذف می‌شوند.

- هیچ محدودیت مصنوعی برای طول محتوای نوشته وجود ندارد
- بلوک‌های کد با رنگ‌بندی نحوی در تم تیره نمایش داده می‌شوند
- با نوشتن `{{video:ID}}` می‌توانید ویدیوی آپلودشده را درون متن جای دهید

نوشتن خوشی داشته باشید.
"""
            });
        }

        await db.SaveChangesAsync();
    }
}
