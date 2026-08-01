using BlogApp.Services.Performance;
using Microsoft.AspNetCore.Mvc;

namespace BlogApp.Controllers;

public partial class PostsController
{
    private async Task InvalidatePublicCacheAsync()
    {
        try
        {
            var inv = HttpContext.RequestServices.GetService<IOutputCacheInvalidator>();
            if (inv is not null)
                await inv.InvalidateAllPublicAsync();
        }
        catch { /* cache optional */ }
    }
}
