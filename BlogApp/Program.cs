using BlogApp.Data;
using BlogApp.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- Database (everything the blog needs lives in one SQLite file). The connection
// string can be overridden via ConnectionStrings__DefaultConnection (e.g. in Docker,
// pointed at a mounted volume like /app/data/blog.db so posts/media survive restarts). ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=blog.db";
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));

// --- Markdown rendering pipeline (readme-style: tables, fenced code, embeds, etc.) ---
builder.Services.AddSingleton<MarkdownService>();

// --- SEO / AEO meta + structured-data helper ---
builder.Services.AddSingleton<SeoService>();

// --- MVC ---
builder.Services.AddControllersWithViews();

// --- Simple cookie auth so only the developer/author can create & edit posts ---
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
    });

// Uploads can be large (video). Raise the request body size limit.
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 500L * 1024 * 1024; // 500 MB
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 500L * 1024 * 1024;
});

var app = builder.Build();

// Make sure the folder for the SQLite file exists — matters on first run against a fresh
// Docker volume mount, where the parent directory may not exist yet.
var dbDirectory = Path.GetDirectoryName(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString).DataSource);
if (!string.IsNullOrEmpty(dbDirectory))
    Directory.CreateDirectory(dbDirectory);

// Auto-create the SQLite database file + schema on first run (no `dotnet ef` step required
// to get started). Once you want proper incremental migrations, run:
//   dotnet ef migrations add InitialCreate
//   dotnet ef database update
// and swap EnsureCreated() below for db.Database.Migrate().
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();
    await DbSeeder.SeedAsync(db);
}

// HTTPS redirection/HSTS assume Kestrel itself terminates TLS. In the Docker image, TLS is
// normally terminated by a reverse proxy (nginx, Traefik, a cloud load balancer) in front of
// the plain-HTTP container, so this is disabled there via the ForceHttps=false env var.
var forceHttps = builder.Configuration.GetValue("ForceHttps", true);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    if (forceHttps) app.UseHsts();
}

if (forceHttps) app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "post-details",
    pattern: "post/{slug}",
    defaults: new { controller = "Posts", action = "Details" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
