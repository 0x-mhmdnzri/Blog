using Serilog;
using Serilog.Events;

namespace AVICRM.Logging;

public static class SerilogBootstrap
{
    public const string CorrelationHeader = "X-Correlation-Id";

    // Short, readable one-liners for the terminal.
    private const string ConsoleTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";

    public static void CreateBootstrapLogger()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: ConsoleTemplate)
            .CreateBootstrapLogger();
    }

    public static void Configure(HostBuilderContext context, IServiceProvider services, LoggerConfiguration configuration)
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: ConsoleTemplate);
    }
}
