using BlogApp.Data;
using BlogApp.Developer.Domain;
using BlogApp.Developer.Messaging;
using BlogApp.Models;
using BlogApp.Models.ViewModels;
using BlogApp.Services;
using BlogApp.Services.Analytics;
using BlogApp.Services.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers;

public partial class PostsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly MarkdownService _markdown;
    private readonly SeoService _seo;
    private readonly AnalyticsBroadcaster _broadcaster;
    private readonly AiContentService _ai;
    private readonly INotificationService _notify;
    private readonly IAnalyticsTracker _analytics;
    private readonly ICultureService _culture;
    private readonly IDomainEventPublisher _events;
    private readonly ILogger<PostsController> _logger;

    public PostsController(
        ApplicationDbContext db,
        MarkdownService markdown,
        SeoService seo,
        AnalyticsBroadcaster broadcaster,
        AiContentService ai,
        INotificationService notify,
        IAnalyticsTracker analytics,
        ICultureService culture,
        IDomainEventPublisher events,
        ILogger<PostsController> logger)
    {
        _db = db;
        _markdown = markdown;
        _seo = seo;
        _broadcaster = broadcaster;
        _ai = ai;
        _notify = notify;
        _analytics = analytics;
        _culture = culture;
        _events = events;
        _logger = logger;
    }
}
