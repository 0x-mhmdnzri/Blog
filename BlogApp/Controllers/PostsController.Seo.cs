using BlogApp.Models;
using BlogApp.Services.Seo;

namespace BlogApp.Controllers;

public partial class PostsController
{
    private void ApplyPostSeo(Post post)
    {
        var desc = PostSeoMeta.BuildDescription(post, _markdown);
        var keywords = PostSeoMeta.BuildKeywords(post);
        var canonical = PostSeoMeta.BuildCanonical(Request, post);

        ViewData["Title"] = post.Title;
        ViewData["Description"] = desc;
        ViewData["Keywords"] = keywords;
        ViewData["Canonical"] = canonical;
        ViewData["OgType"] = "article";
        ViewData["NoIndex"] = !post.IsPublished;

        var authorName = post.Author?.DisplayName ?? _seo.AuthorName;
        ViewData["Author"] = authorName;
        ViewData["ArticlePublished"] = (post.PublishedAtUtc ?? post.CreatedAtUtc).ToString("o");
        ViewData["ArticleModified"] = post.UpdatedAtUtc.ToString("o");
        ViewData["ArticleSection"] = post.Category?.Name;
        ViewData["ArticleTags"] = post.PostTags?
            .Where(pt => pt.Tag != null)
            .Select(pt => pt.Tag!.Name)
            .ToArray() ?? Array.Empty<string>();

        string? ogImage = null;
        if (post.CoverMediaAssetId is > 0)
            ogImage = $"{Request.Scheme}://{Request.Host}/media/{post.CoverMediaAssetId}";
        else
        {
            var cards = HttpContext.RequestServices.GetService(typeof(IPostOgCardService)) as IPostOgCardService;
            if (cards is not null)
                ogImage = cards.GetCardUrl(post, Request);
        }
        ViewData["OgImage"] = ogImage;
        ViewData["OgImageAlt"] = post.Title;

        ViewBag.PostJsonLd = _seo.BuildPostJsonLd(post, canonical, ogImage);
        ViewBag.BreadcrumbJsonLd = _seo.BuildBreadcrumbJsonLd(
            ("خانه", $"{Request.Scheme}://{Request.Host}/"),
            (post.Category?.Name ?? "بلاگ", post.Category != null
                ? $"{Request.Scheme}://{Request.Host}/{post.LanguageCode}/category/{post.Category.Slug}"
                : $"{Request.Scheme}://{Request.Host}/{post.LanguageCode}"),
            (post.Title, canonical));
    }
}
