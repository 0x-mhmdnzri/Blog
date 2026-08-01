using BlogApp.Models;
using BlogApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace BlogApp.ViewComponents;

public class SiteAnnounceViewComponent : ViewComponent
{
    private readonly ISiteConfigService _config;

    public SiteAnnounceViewComponent(ISiteConfigService config) => _config = config;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var on = await _config.GetBoolAsync(SiteSettingKeys.AnnouncementEnabled);
        var text = await _config.GetAsync(SiteSettingKeys.AnnouncementText);
        if (!on || string.IsNullOrWhiteSpace(text))
            return Content(string.Empty);

        return View(new SiteAnnounceModel
        {
            Text = text!,
            Style = await _config.GetAsync(SiteSettingKeys.AnnouncementStyle) ?? "info",
            Version = await _config.GetAsync(SiteSettingKeys.AnnouncementVersion) ?? "0"
        });
    }
}

public class SiteAnnounceModel
{
    public string Text { get; set; } = "";
    public string Style { get; set; } = "info";
    public string Version { get; set; } = "0";
}
