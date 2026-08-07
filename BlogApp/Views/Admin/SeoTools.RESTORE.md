# Restore SeoTools.cshtml

The full SeoTools view was temporarily truncated during P0.1.

Restore from commit `e6d47bb` then re-add the Crawl tab:

```bash
git checkout e6d47bb -- BlogApp/Views/Admin/SeoTools.cshtml
```

Then in the tabs nav, after the IndexNow tab, add:

```html
<a class="st-tab @TabClass("crawl")" asp-action="SeoTools" asp-route-tab="crawl">Crawl</a>
```

And at the end of the file:

```html
@if (tab == "crawl")
{
    <partial name="_SeoCrawlTab" model="Model.Crawl" />
}
```

The partial `_SeoCrawlTab.cshtml` is already on `dev`.
