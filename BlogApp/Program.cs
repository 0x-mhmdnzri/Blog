using System.IO.Compression;
using System.Text;
using System.Threading.RateLimiting;
using BlogApp;
using BlogApp.Api.Auth;
using BlogApp.Api.Validation;
using BlogApp.Data;
using BlogApp.Developer;
using BlogApp.Logging;
using BlogApp.Middleware;
using BlogApp.Models;
using BlogApp.Services;
using BlogApp.Services.Analytics;
using BlogApp.Services.Messaging;
using Blog.Infrastructure.Middleware;
using FluentValidation;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using Serilog;
using SameSiteMode = Microsoft.AspNetCore.Http.SameSiteMode;

SerilogBootstrap.CreateBootstrapLogger();

try
{
    try { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); }
    catch { /* optional */ }

    Console.OutputEncoding = Encoding.UTF8;

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, cfg) => SerilogBootstrap.Configure(ctx, services, cfg));

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=blog.db;Cache=Shared;Pooling=True;Default Timeout=30";

    var maxPool = builder.Configuration.GetValue("Database:MaxPoolSize", 100);
    var minPool = builder.Configuration.GetValue("Database:MinPoolSize", 0);
    var cmdTimeout = builder.Configuration.GetValue("Database:CommandTimeoutSeconds", 30);

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseSqlite(connectionString, sqlite =>
        {
            sqlite.CommandTimeout(cmdTimeout);
        });

        options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        options.EnableSensitiveDataLogging(false);
        options.EnableDetailedErrors(builder.Environment.IsDevelopment());
    });

    Log.Information("EF NoTracking default, LazyLoading=off, CommandTimeout={Timeout}s, Pool Max={Max} Min={Min}",
        cmdTimeout, maxPool, minPool);

    builder.Services.AddMemoryCache();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddDataProtection();
    builder.Services.AddSingleton<IApiTokenProtector, ApiTokenProtector>();

    builder.Services.AddBlogPerformance(builder.Configuration);

    // Clean architecture layers: Domain / Application / Infrastructure (Developer Features)
    builder.Services.AddDeveloperFeatures(builder.Configuration);

    builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 10;
            options.Password.RequiredUniqueChars = 4;
            options.User.RequireUniqueEmail = true;
            options.User.AllowedUserNameCharacters =
                "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

            options.SignIn.RequireConfirmedAccount = false;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

    builder.Services.AddAuthentication()
        .AddScheme<ApiKeyAuthOptions, ApiKeyAuthHandler>(ApiKeyDefaults.Scheme, _ => { });

    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "Blog.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.IsEssential = true;
    });

    builder.Services.AddAntiforgery(options =>
    {
        options.HeaderName = "X-CSRF-TOKEN";
        options.Cookie.Name = "Blog.CSRF";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });

    builder.Services.AddValidatorsFromAssemblyContaining<ApiCommentCreateValidator>();

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (ctx, token) =>
        {
            ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            ctx.HttpContext.Response.ContentType = "application/json; charset=utf-8";
            await ctx.HttpContext.Response.WriteAsync(
                "{\"error\":\"rate_limited\",\"detail\":\"Too many requests\"}", token);

            try
            {
                var keyClaim = ctx.HttpContext.User.FindFirst("api_key_id")?.Value;
                if (int.TryParse(keyClaim, out var apiKeyId) && apiKeyId > 0)
                {
                    var db = ctx.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                    await ApiKeyAbuseService.RegisterRateLimitStrikeAsync(db, apiKeyId, "rate_limit_429");
                }
            }
            catch { /* never fail the 429 response */ }
        };

        options.AddPolicy("global", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 120,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

        options.AddPolicy("api", httpContext =>
        {
            var keyId = httpContext.User.FindFirst("api_key_id")?.Value
                        ?? httpContext.Connection.RemoteIpAddress?.ToString()
                        ?? "anon";
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: "api:" + keyId,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
        });

        options.AddPolicy("login", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: "login:" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 8,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

        options.AddPolicy("upload", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: "upload:" + (httpContext.User.Identity?.Name
                    ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

        options.AddPolicy("comment", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: "comment:" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 6,
                    Window = TimeSpan.FromMinutes(5),
                    QueueLimit = 0
                }));
    });

    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
        options.Providers.Add<BrotliCompressionProvider>();
        options.Providers.Add<GzipCompressionProvider>();
        options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
        {
            "application/javascript",
            "application/json",
            "application/xml",
            "text/xml",
            "image/svg+xml",
            "application/ld+json",
            "application/rss+xml",
            "application/atom+xml"
        });
    });
    builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
    builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

    builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
    builder.Services.Configure<SmsOptions>(builder.Configuration.GetSection("Sms"));
    builder.Services.Configure<PushOptions>(builder.Configuration.GetSection("Push"));
    builder.Services.Configure<DigestOptions>(builder.Configuration.GetSection("Digest"));
    builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));

    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
    builder.Services.AddSingleton<ISmsSender, ConfigurableSmsSender>();
    builder.Services.AddSingleton<IPushSender, NoOpPushSender>();
    builder.Services.AddSingleton<INotificationEventBus, NotificationEventBus>();
    builder.Services.AddSingleton<NotificationHub>();
    builder.Services.AddScoped<INotificationService, NotificationService>();
    builder.Services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
    builder.Services.AddHostedService<WeeklyDigestHostedService>();
    builder.Services.AddHostedService<NotificationRealtimeHostedService>();
    builder.Services.AddHostedService<NotificationSchedulerHostedService>();
    builder.Services.AddHostedService<NewsletterCampaignHostedService>();

    builder.Services.AddSingleton<MarkdownService>();
    builder.Services.AddSingleton<SeoService>();
    builder.Services.AddSingleton<AnalyticsBroadcaster>();
    builder.Services.AddScoped<IAnalyticsTracker, AnalyticsTracker>();
    builder.Services.AddScoped<AiContentService>();
    builder.Services.AddScoped<BrokenLinkService>();
    builder.Services.AddScoped<ISiteConfigService, SiteConfigService>();
    builder.Services.AddScoped<IAuditService, AuditService>();
    builder.Services.AddScoped<ICultureService, CultureService>();
    builder.Services.AddScoped<IUiTranslator, UiTranslatorService>();
    builder.Services.AddScoped<IMembershipService, MembershipService>();
    builder.Services.AddScoped<INewsletterService, NewsletterService>();
    builder.Services.AddScoped<MentionsService>();
    builder.Services.AddScoped<IThemeService, ThemeService>();

    builder.Services.AddApiTopicBus(builder.Configuration);

    builder.Services.AddControllersWithViews(options =>
    {
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    });

    builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
    {
        options.MultipartBodyLengthLimit = SafeUpload.MaxVideoBytes;
        options.ValueLengthLimit = 1024 * 1024;
        options.MemoryBufferThreshold = 64 * 1024;
    });

    builder.WebHost.ConfigureKestrel(options =>
    {
        options.AddServerHeader = false;
        options.Limits.MaxRequestBodySize = SafeUpload.MaxVideoBytes;
        options.Limits.MaxConcurrentConnections = 500;
        options.Limits.MaxConcurrentUpgradedConnections = 100;
        options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
        options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(20);
        options.Limits.MaxRequestHeadersTotalSize = 32 * 1024;
        options.Limits.MaxRequestLineSize = 8 * 1024;
        options.Limits.Http2.MaxStreamsPerConnection = 50;
        options.Limits.MinRequestBodyDataRate = null;
        options.Limits.MinResponseDataRate = null;
    });

    var app = builder.Build();

    Log.Information("BlogApp starting Environment={Environment} ContentRoot={ContentRoot}",
        app.Environment.EnvironmentName, app.Environment.ContentRootPath);

    var forwardedHeadersOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    };
    forwardedHeadersOptions.KnownNetworks.Clear();
    forwardedHeadersOptions.KnownProxies.Clear();
    app.UseForwardedHeaders(forwardedHeadersOptions);

    var dbDirectory = Path.GetDirectoryName(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString).DataSource);
    if (!string.IsNullOrEmpty(dbDirectory))
        Directory.CreateDirectory(dbDirectory);

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var siteConfig = scope.ServiceProvider.GetRequiredService<ISiteConfigService>();
        var uiT = scope.ServiceProvider.GetRequiredService<IUiTranslator>();
        var themes = scope.ServiceProvider.GetRequiredService<IThemeService>();

        try
        {
            db.Database.EnsureCreated();
            await SchemaBootstrap.EnsureAsync(db);
            await DbSeeder.SeedAsync(db, userManager, roleManager, config);
            await siteConfig.EnsureDefaultsAsync();
            await uiT.EnsureSeedAsync();
            await themes.EnsureSystemThemesAsync();
            var packResult = await themes.ImportFromDirectoryAsync();
            Log.Information("Database ready Path={DbPath}; theme packs +{Imported} ~{Updated} skip={Skipped}",
                connectionString, packResult.Imported, packResult.Updated, packResult.Skipped);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Database bootstrap failed");
            throw;
        }
    }

    // Plugins, widgets, extension pipeline bootstrap
    await app.UseDeveloperFeaturesAsync();

    var forceHttps = builder.Configuration.GetValue("ForceHttps", false);

    app.UseExceptionHandler("/Home/Error?statusCode=500");
    app.UseStatusCodePagesWithReExecute("/Home/Error", "?statusCode={0}");

    if (forceHttps && !app.Environment.IsDevelopment())
        app.UseHsts();

    if (forceHttps)
        app.UseHttpsRedirection();

    app.UseBlogExtensionSlot("early");

    app.UseMiddleware<SecurityHeadersMiddleware>();
    app.UseResponseCompression();
    app.UseMiddleware<RequestLoggingMiddleware>();

    app.Use(async (ctx, next) =>
    {
        if (HttpMethods.IsTrace(ctx.Request.Method) ||
            string.Equals(ctx.Request.Method, "TRACK", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        ctx.Response.OnStarting(() =>
        {
            var ct = ctx.Response.ContentType;
            if (!string.IsNullOrEmpty(ct)
                && (ct.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
                    || ct.StartsWith("application/json", StringComparison.OrdinalIgnoreCase)
                    || ct.StartsWith("application/javascript", StringComparison.OrdinalIgnoreCase))
                && !ct.Contains("charset", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Response.ContentType = ct + "; charset=utf-8";
            }
            return Task.CompletedTask;
        });
        await next();
    });

    var staticCache = new StaticFileOptions
    {
        OnPrepareResponse = ctx =>
        {
            var headers = ctx.Context.Response.Headers;
            headers[HeaderNames.CacheControl] = "public,max-age=604800,immutable";
            headers["X-Content-Type-Options"] = "nosniff";
        }
    };
    app.UseStaticFiles(staticCache);

    app.UseBlogPerformance(builder.Configuration);

    app.UseMiddleware<CultureMiddleware>();

    app.UseRouting();

    app.UseBlogExtensionSlot("pre-auth");

    app.UseMiddleware<RedirectMiddleware>();

    app.UseAuthentication();
    app.UseAuthorization();

    app.UseBlogExtensionSlot("post-auth");

    app.UseRateLimiter();

    app.UseMiddleware<ApiRequestLoggingMiddleware>();

    app.UseMiddleware<MaintenanceMiddleware>();

    app.UseBlogExtensionSlot("pre-endpoint");

    app.MapGet("/health", () => Results.Text("ok", "text/plain; charset=utf-8"))
        .AllowAnonymous()
        .DisableRateLimiting();

    app.MapGet("/ready", async (ApplicationDbContext db) =>
        {
            try
            {
                if (await db.Database.CanConnectAsync())
                    return Results.Text("ready", "text/plain; charset=utf-8");
            }
            catch { /* fall through */ }
            return Results.Text("not-ready", "text/plain; charset=utf-8", statusCode: 503);
        })
        .AllowAnonymous()
        .DisableRateLimiting();

    // /healthz, /healthz/ready, /metrics, /widgets/{zone}, /dev/*
    app.MapDeveloperEndpoints();

    app.MapControllers().RequireRateLimiting("global");

    app.MapControllerRoute(
            name: "post-details",
            pattern: "post/{slug}",
            defaults: new { controller = "Posts", action = "Details" })
        .RequireRateLimiting("global");

    app.MapControllerRoute(
            name: "author-profile",
            pattern: "author/{userName}",
            defaults: new { controller = "Account", action = "PublicProfile" })
        .RequireRateLimiting("global");

    app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}")
        .RequireRateLimiting("global");

    Log.Information("BlogApp listening ForceHttps={ForceHttps} health=/health|/healthz metrics=/metrics",
        forceHttps);
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
