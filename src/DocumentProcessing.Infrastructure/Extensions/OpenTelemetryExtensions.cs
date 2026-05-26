using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackExchange.Redis;

namespace DocumentProcessing.Infrastructure.Extensions;

/// <summary>DataDog/OTLP tracing setup wired through OpenTelemetry.</summary>
public static class OpenTelemetryExtensions
{
    /// <summary>
    /// Registers OpenTelemetry tracing with OTLP exporter pointed at the DataDog agent,
    /// instrumenting ASP.NET Core, HttpClient, EF Core, StackExchange.Redis, and the Azure SDK.
    /// </summary>
    public static IServiceCollection AddDataDogTracing(this IServiceCollection services, IConfiguration configuration)
    {
        var serviceName = configuration["DataDog:ServiceName"] ?? "document-processing";
        var otlpEndpoint = configuration["DataDog:OtlpEndpoint"] ?? "http://localhost:4317";

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = configuration["ASPNETCORE_ENVIRONMENT"] ?? "production",
                    ["service.version"] = configuration["APP_VERSION"] ?? "1.0.0"
                }))
            .WithTracing(tracing => tracing
                .AddSource("DocumentProcessing.Infrastructure")
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health");
                })
                .AddHttpClientInstrumentation(options => options.RecordException = true)
                .AddEntityFrameworkCoreInstrumentation(options =>
                {
                    options.SetDbStatementForText = true;
                    options.SetDbStatementForStoredProcedure = true;
                })
                .AddRedisInstrumentation(
                    sp => (ConnectionMultiplexer)sp.GetRequiredService<IConnectionMultiplexer>(),
                    options => options.SetVerboseDatabaseStatements = false)
                .AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint)));

        return services;
    }
}
