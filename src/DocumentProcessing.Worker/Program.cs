using DocumentProcessing.Infrastructure.Extensions;
using DocumentProcessing.Worker;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting DocumentProcessing.Worker");

    var builder = Host.CreateApplicationBuilder(args);

    // Serilog — full sink/enricher config comes from appsettings.json Serilog section
    builder.Services.AddSerilog((_, loggerConfig) =>
        loggerConfig
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext());

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
catch (Exception ex) when (ex is not OperationCanceledException and not HostAbortedException)
{
    Log.Fatal(ex, "DocumentProcessing.Worker terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
