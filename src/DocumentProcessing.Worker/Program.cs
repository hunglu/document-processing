using DocumentProcessing.Infrastructure.Extensions;
using DocumentProcessing.Worker;
using Microsoft.ApplicationInsights.Extensibility;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting DocumentProcessing.Worker");

    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog((services, loggerConfig) =>
        loggerConfig
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.ApplicationInsights(
                builder.Configuration["ApplicationInsights:ConnectionString"],
                TelemetryConverter.Traces));

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddDataDogTracing(builder.Configuration);
    builder.Services.AddSingleton<DocumentProcessingPipeline>();
    builder.Services.AddHostedService<ServiceBusWorker>();

    var host = builder.Build();

    // Migrate database on startup
    using (var scope = host.Services.CreateScope())
    {
        var db = scope.ServiceProvider
            .GetRequiredService<DocumentProcessing.Infrastructure.Persistence.ApplicationDbContext>();
        await db.Database.MigrateAsync();
    }

    await host.RunAsync();
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    Log.Fatal(ex, "DocumentProcessing.Worker terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
