using BlogApp.Data;
using BlogApp.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- Database (everything the blog needs lives in one SQLite file: blog.db) ---
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=blog.db"));

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

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
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
