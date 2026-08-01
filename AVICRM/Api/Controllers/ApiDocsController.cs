using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AVICRM.Api.Controllers;

[ApiController]
[Route("api")]
[AllowAnonymous]
[EnableRateLimiting("global")]
public class ApiDocsController : ControllerBase
{
    [HttpGet("docs")]
    public IActionResult Docs()
    {
        var doc = new
        {
            name = "AVICRM Public API",
            version = "v1",
            authentication = new
            {
                type = "API Key (PAT)",
                headers = new[] { "X-Api-Key: blog_…", "Authorization: Bearer blog_…" },
                create = "Account → API Keys (after login), or SuperAdmin → Admin API Keys"
            },
            rateLimit = new
            {
                policy = "api",
                note = "Exceeding limits increments abuse strikes; 5 strikes auto-ban the key."
            },
            validation = "FluentValidation on all write DTOs (length, XSS, SSRF guards)",
            endpoints = new object[]
            {
                new { method = "GET", path = "/api/v1/posts", scope = "read", query = "page,pageSize,q,lang,tag,category" },
                new { method = "GET", path = "/api/v1/posts/{slug}", scope = "read", query = "lang" },
                new { method = "POST", path = "/api/v1/comments", scope = "write", body = "{ postId, authorName, body }" },
                new { method = "GET|POST|DELETE", path = "/api/v1/webhooks", scope = "webhooks" },
                new { method = "POST", path = "/api/graphql", scope = "read", body = "{ query: \"{ posts(limit:5) { title slug } }\" }" },
                new { method = "GET", path = "/feed/rss", auth = false },
                new { method = "GET", path = "/feed/atom", auth = false },
                new { method = "GET", path = "/api/docs", auth = false }
            },
            scopes = new[] { "read", "write", "webhooks" }
        };
        return Ok(doc);
    }
}
