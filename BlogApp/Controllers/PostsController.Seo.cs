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
        ViewData["NoIndex"] = !post.IsPublished || post.IsDeleted;

        var authorName = post.Author?.DisplayName ?? _seo.AuthorName;
        ViewData["Author"] = authorName;
        ViewData["ArticlePublished"] = (post.PublishedAtUtc ?? post.CreatedAtUtc).ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        ViewData["ArticleModified"] = post.UpdatedAtUtc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        ViewData["ArticleSection"] = post.Category?.Name;
        ViewData["ArticleTags"] = post.PostTags?
            .Where(pt => pt.Tag != null)
            .Select(pt => pt.Tag!.Name)
            .ToArray() ?? Array.Empty<string>();

        // Prefer generated OG card (title, views, likes, read time) for richer shares.
        string? ogImage = null;
        var cards = HttpContext.RequestServices.GetService(typeof(IPostOgCardService)) as IPostOgCardService;
        if (cards is not null)
            ogImage = cards.GetCardUrl(post, Request);
        else if (post.CoverMediaAssetId is > 0)
            ogImage = $"{Request.Scheme}://{Request.Host}/media/{post.CoverMediaAssetId}";
        ViewData["OgImage"] = ogImage;
        ViewData["OgImageAlt"] = post.Title;

        // hreflang alternates for translation group
        if (post.TranslationGroupId is int gid && gid > 0)
        {
            // filled by Details action when siblings are loaded; safe empty default
        }

        ViewBag.PostJsonLd = _seo.BuildPostJsonLd(post, canonical, ogImage);
        ViewBag.BreadcrumbJsonLd = _seo.BuildBreadcrumbJsonLd(
            ("Home", $"{Request.Scheme}://{Request.Host}/"),
            (post.Category?.Name ?? "Blog", post.Category != null
                ? $"{Request.Scheme}://{Request.Host}/{post.LanguageCode}/category/{post.Category.Slug}"
                : $"{Request.Scheme}://{Request.Host}/{post.LanguageCode}"),
            (post.Title, canonical));
    }
}
