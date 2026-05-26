using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace DocumentProcessing.Infrastructure.Extensions;

/// <summary>Serilog bootstrapping helpers.</summary>
public static class SerilogExtensions
{
    /// <summary>
    /// Configures Serilog on the host builder with console + Application Insights sinks,
    /// structured enrichers, and reads additional config from the <c>Serilog</c> section.
    /// </summary>
    public static IHostBuilder AddSerilogWithAppInsights(this IHostBuilder builder)
    {
        return builder.UseSerilog((context, _, loggerConfig) =>
        {
            loggerConfig
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .WriteTo.Console(outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.ApplicationInsights(
                    context.Configuration["ApplicationInsights:ConnectionString"],
                    TelemetryConverter.Traces,
                    LogEventLevel.Information);
        });
    }
}
