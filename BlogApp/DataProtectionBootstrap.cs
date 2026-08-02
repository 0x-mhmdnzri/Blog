using Microsoft.AspNetCore.DataProtection;
using Serilog;

namespace BlogApp;

/// <summary>
/// Persists ASP.NET Data Protection keys so antiforgery / auth cookies
/// survive process restarts and Docker image rebuilds (keys live on the data volume).
/// </summary>
public static class DataProtectionBootstrap
{
    public static void AddBlogDataProtection(this WebApplicationBuilder builder, string connectionString)
    {
        var keysPath = builder.Configuration["DataProtection:KeysPath"];
        if (string.IsNullOrWhiteSpace(keysPath))
        {
            try
            {
                var ds = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString).DataSource;
                var dbDir = Path.GetDirectoryName(Path.GetFullPath(string.IsNullOrWhiteSpace(ds) ? "blog.db" : ds));
                keysPath = Path.Combine(string.IsNullOrEmpty(dbDir) ? builder.Environment.ContentRootPath : dbDir, "dp-keys");
            }
            catch
            {
                keysPath = Path.Combine(builder.Environment.ContentRootPath, "data", "dp-keys");
            }
        }

        Directory.CreateDirectory(keysPath);
        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
            .SetApplicationName(builder.Configuration["DataProtection:ApplicationName"] ?? "BlogApp");
        Log.Information("DataProtection keys Path={KeysPath}", keysPath);
    }
}
