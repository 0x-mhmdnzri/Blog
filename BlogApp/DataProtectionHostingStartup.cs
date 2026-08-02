using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

[assembly: HostingStartup(typeof(BlogApp.DataProtectionHostingStartup))]

namespace BlogApp;

/// <summary>
/// Runs automatically at host build. Persists Data Protection keys next to SQLite
/// (Docker: /app/data/dp-keys on volume blog_data) so antiforgery tokens still decrypt
/// after container rebuilds. Also applies when Program only calls AddDataProtection().
/// </summary>
public sealed class DataProtectionHostingStartup : IHostingStartup
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices((context, services) =>
        {
            var config = context.Configuration;
            var keysPath = config["DataProtection:KeysPath"];

            if (string.IsNullOrWhiteSpace(keysPath))
            {
                var cs = config.GetConnectionString("DefaultConnection")
                         ?? "Data Source=blog.db";
                try
                {
                    var ds = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(cs).DataSource;
                    var dbDir = Path.GetDirectoryName(Path.GetFullPath(string.IsNullOrWhiteSpace(ds) ? "blog.db" : ds));
                    var contentRoot = context.HostingEnvironment.ContentRootPath;
                    keysPath = Path.Combine(string.IsNullOrEmpty(dbDir) ? contentRoot : dbDir, "dp-keys");
                }
                catch
                {
                    keysPath = Path.Combine(context.HostingEnvironment.ContentRootPath, "data", "dp-keys");
                }
            }

            Directory.CreateDirectory(keysPath);
            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
                .SetApplicationName(config["DataProtection:ApplicationName"] ?? "BlogApp");
        });
    }
}
