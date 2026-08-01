using AVICRM.Data;
using AVICRM.Developer.Domain;
using AVICRM.Developer.Messaging;
using AVICRM.Models;
using AVICRM.Models.ViewModels;
using AVICRM.Services;
using AVICRM.Services.Analytics;
using AVICRM.Services.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AVICRM.Controllers;

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
