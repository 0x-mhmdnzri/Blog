using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace BlogApp.Logging;

/// <summary>
/// Builds a high-performance Serilog pipeline oriented at ELK (Filebeat → Logstash/Elastic).
/// Console + rolling file emit Compact JSON; sinks are wrapped in Async to keep the request path free.
/// </summary>
public static class SerilogBootstrap
{
    public const string CorrelationHeader = "X-Correlation-Id";

    public static void CreateBootstrapLogger()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Async(a => a.Console(new CompactJsonFormatter()))
            .CreateBootstrapLogger();
    }

    public static void Configure(HostBuilderContext context, IServiceProvider services, LoggerConfiguration configuration)
    {
        var env = context.HostingEnvironment;
        var contentRoot = env.ContentRootPath;
        var logsDir = Path.Combine(contentRoot, "logs");
        Directory.CreateDirectory(logsDir);

        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .Enrich.WithThreadId()
            .Enrich.WithProperty("Application", "BlogApp")
            .Enrich.WithProperty("Environment", env.EnvironmentName)
            // Human-readable console in Development; Compact JSON everywhere else (ELK).
            .WriteTo.Async(a =>
            {
                if (env.IsDevelopment())
                {
                    a.Console(
                        outputTemplate:
                        "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}{NewLine}  {Message:lj}{NewLine}{Exception}");
                }
                else
                {
                    a.Console(new CompactJsonFormatter());
                }
            })
            // Rolling JSON files — ship with Filebeat / fluent-bit into Elasticsearch.
            .WriteTo.Async(a => a.File(
                new CompactJsonFormatter(),
                path: Path.Combine(logsDir, "blog-.json"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true,
                buffered: true,
                flushToDiskInterval: TimeSpan.FromSeconds(2)));
    }
}
