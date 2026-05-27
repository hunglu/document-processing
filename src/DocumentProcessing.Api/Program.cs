using DocumentProcessing.Api.Extensions;
using DocumentProcessing.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Serilog;

// Bootstrap logger before host is built
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting DocumentProcessing.Api");

    var builder = WebApplication.CreateBuilder(args);

    // Serilog — full sink/enricher config comes from appsettings.json Serilog section
    builder.Host.UseSerilog((context, _, loggerConfig) =>
        loggerConfig
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext());

    // Infrastructure services (EF, Redis, Blob, ServiceBus, CQRS)
    builder.Services.AddInfrastructure(builder.Configuration);

    // OpenTelemetry / DataDog
    builder.Services.AddDataDogTracing(builder.Configuration);

    // MVC Controllers
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new() { Title = "Document Processing API", Version = "v1" });
    });

    // Rate limiting
    builder.Services.AddApiRateLimiting();

    // Health checks
    builder.Services.AddApiHealthChecks(builder.Configuration);

    // CORS (restrictive by default)
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
            policy.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [])
                  .AllowAnyHeader()
                  .AllowAnyMethod());
    });

    // ProblemDetails for RFC 7807 responses
    builder.Services.AddProblemDetails();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseMiddleware<DocumentProcessing.Api.Middleware.CorrelationIdMiddleware>();
    app.UseMiddleware<DocumentProcessing.Api.Middleware.GlobalExceptionMiddleware>();

    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            diagnosticContext.Set("CorrelationId",
                httpContext.Items["CorrelationId"]?.ToString() ?? string.Empty);
        };
    });

    app.UseCors();
    app.UseRateLimiter();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });

    // Auto-migrate on startup (dev only; use release pipeline migrations in production)
    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocumentProcessing.Infrastructure.Persistence.ApplicationDbContext>();
        await db.Database.MigrateAsync();
    }

    await app.RunAsync();
}
catch (Exception ex) when (ex is not OperationCanceledException and not HostAbortedException)
{
    Log.Fatal(ex, "DocumentProcessing.Api terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
