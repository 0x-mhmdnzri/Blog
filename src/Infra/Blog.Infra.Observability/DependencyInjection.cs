namespace Blog.Infra.Observability;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

public static class DependencyInjection
{
    public static IServiceCollection AddObservabilityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var serviceName = configuration["OpenTelemetry:ServiceName"] ?? "BlogApp";
        var otlp = configuration["OpenTelemetry:OtlpEndpoint"];
        var prometheus = configuration.GetValue("OpenTelemetry:Prometheus", true);

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(t =>
            {
                t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();
                if (!string.IsNullOrWhiteSpace(otlp))
                    t.AddOtlpExporter(o => o.Endpoint = new Uri(otlp));
            })
            .WithMetrics(m =>
            {
                m.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();
                if (prometheus) m.AddPrometheusExporter();
                if (!string.IsNullOrWhiteSpace(otlp))
                    m.AddOtlpExporter(o => o.Endpoint = new Uri(otlp));
            });

        services.AddHealthChecks();
        return services;
    }

    public static IEndpointRouteBuilder MapObservabilityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/healthz");
        endpoints.MapHealthChecks("/healthz/ready");
        endpoints.MapPrometheusScrapingEndpoint("/metrics");
        return endpoints;
    }
}
