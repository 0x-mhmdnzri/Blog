using System.Text;
using BlogApp.Data;
using BlogApp.Logging;
using BlogApp.Middleware;
using BlogApp.Models;
using BlogApp.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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

    builder.Services.AddSingleton<MarkdownService>();
    builder.Services.AddSingleton<SeoService>();
    builder.Services.AddSingleton<AnalyticsBroadcaster>();
    builder.Services.AddScoped<AiContentService>();
    builder.Services.AddScoped<BrokenLinkService>();

    builder.Services.AddControllersWithViews();

    builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
    {
        options.MultipartBodyLengthLimit = 500L * 1024 * 1024;
    });
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxRequestBodySize = 500L * 1024 * 1024;
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

    app.UseStaticFiles();
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

    Log.Information("BlogApp listening");
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
