using System.IO.Compression;
using System.Text;
using BlogApp.Data;
using BlogApp.Logging;
using BlogApp.Middleware;
using BlogApp.Models;
using BlogApp.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using Serilog;

SerilogBootstrap.CreateBootstrapLogger();

try
{
    try
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
    catch
    {
        // Optional: package missing in some publish layouts — UTF-8 still works as default.
    }

    Console.OutputEncoding = Encoding.UTF8;

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, cfg) => SerilogBootstrap.Configure(ctx, services, cfg));

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=blog.db";
    builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));

    builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 6;
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
    });

    // ---- HTTP: Brotli + Gzip (smaller payloads → lower bandwidth, faster TTFB on text) ----
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
            "application/ld+json"
        });
    });
    builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
    builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

    builder.Services.AddSingleton<MarkdownService>();
    builder.Services.AddSingleton<SeoService>();
    builder.Services.AddSingleton<AnalyticsBroadcaster>();
    builder.Services.AddScoped<AiContentService>();
    builder.Services.AddScoped<BrokenLinkService>();

    builder.Services.AddControllersWithViews();

    builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
    {
        options.MultipartBodyLengthLimit = 500L * 1024 * 1024;
        options.ValueLengthLimit = int.MaxValue;
        options.MemoryBufferThreshold = 64 * 1024;
    });

    // ---- Kestrel: throughput + safety knobs ----
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.AddServerHeader = false;
        options.Limits.MaxRequestBodySize = 500L * 1024 * 1024;
        options.Limits.MaxConcurrentConnections = 1000;
        options.Limits.MaxConcurrentUpgradedConnections = 100;
        options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
        options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
        options.Limits.Http2.MaxStreamsPerConnection = 100;
        options.Limits.Http2.KeepAlivePingTimeout = TimeSpan.FromSeconds(30);
        options.Limits.MinRequestBodyDataRate = null; // large media uploads over slow links
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

        try
        {
            db.Database.EnsureCreated();
            await SchemaBootstrap.EnsureAsync(db);
            await DbSeeder.SeedAsync(db, userManager, roleManager, config);
            Log.Information("Database ready Path={DbPath}", connectionString);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Database bootstrap failed");
            throw;
        }
    }

    var forceHttps = builder.Configuration.GetValue("ForceHttps", false);

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        if (forceHttps) app.UseHsts();
    }

    if (forceHttps) app.UseHttpsRedirection();

    // Compression must run early so static files + MVC responses are compressed.
    app.UseResponseCompression();

    app.UseMiddleware<RequestLoggingMiddleware>();

    app.Use(async (ctx, next) =>
    {
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
            // Versioned assets (asp-append-version) can be cached aggressively.
            var headers = ctx.Context.Response.Headers;
            headers[HeaderNames.CacheControl] = "public,max-age=604800,immutable"; // 7 days
        }
    };
    app.UseStaticFiles(staticCache);

    app.UseRouting();

    app.UseMiddleware<RedirectMiddleware>();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllerRoute(
        name: "post-details",
        pattern: "post/{slug}",
        defaults: new { controller = "Posts", action = "Details" });

    app.MapControllerRoute(
        name: "author-profile",
        pattern: "author/{userName}",
        defaults: new { controller = "Account", action = "PublicProfile" });

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    Log.Information("BlogApp listening (compression + Kestrel limits active)");
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
