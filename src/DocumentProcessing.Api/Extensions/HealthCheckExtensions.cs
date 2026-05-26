using DocumentProcessing.Infrastructure.Configuration;
using DocumentProcessing.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DocumentProcessing.Api.Extensions;

/// <summary>Health check registrations for all external dependencies.</summary>
public static class HealthCheckExtensions
{
    /// <summary>Registers health checks for SQL Server, Redis, and Service Bus.</summary>
    public static IServiceCollection AddApiHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is required.");
        var redisConnectionString = configuration[$"{RedisOptions.SectionName}:ConnectionString"]
            ?? throw new InvalidOperationException("Redis:ConnectionString is required.");
        var sbConnectionString = configuration[$"{ServiceBusOptions.SectionName}:ConnectionString"]
            ?? throw new InvalidOperationException("Azure:ServiceBus:ConnectionString is required.");
        var queueName = configuration[$"{ServiceBusOptions.SectionName}:ProcessingQueueName"]
            ?? "document-processing-queue";

        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>(
                name: "sql-server",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready", "db"])
            .AddRedis(
                redisConnectionString,
                name: "redis",
                failureStatus: HealthStatus.Degraded,
                tags: ["ready", "cache"])
            .AddAzureServiceBusQueue(
                sbConnectionString,
                queueName,
                name: "service-bus",
                failureStatus: HealthStatus.Degraded,
                tags: ["ready", "messaging"]);

        return services;
    }
}
