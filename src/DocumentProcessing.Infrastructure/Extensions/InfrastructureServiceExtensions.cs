using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using DocumentProcessing.Contracts.DTOs;
using DocumentProcessing.Core.Commands;
using DocumentProcessing.Core.CQRS;
using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Core.Queries;
using DocumentProcessing.Infrastructure.Caching;
using DocumentProcessing.Infrastructure.Configuration;
using DocumentProcessing.Infrastructure.CQRS;
using DocumentProcessing.Infrastructure.CQRS.Handlers;
using DocumentProcessing.Infrastructure.Messaging;
using DocumentProcessing.Infrastructure.Persistence;
using DocumentProcessing.Infrastructure.Persistence.Repositories;
using DocumentProcessing.Infrastructure.Storage;
using DocumentProcessing.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace DocumentProcessing.Infrastructure.Extensions;

/// <summary>Registers all infrastructure services into the DI container.</summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Adds EF Core, Redis, Azure Blob, Service Bus, CQRS handlers, and dispatchers.
    /// Call from Program.cs to keep the host builder clean.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext(configuration);
        services.AddRedis(configuration);
        services.AddAzureBlob(configuration);
        services.AddAzureServiceBus(configuration);
        services.AddRepositories();
        services.AddCqrs();

        return services;
    }

    private static IServiceCollection AddDbContext(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql =>
                {
                    sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
                    sql.CommandTimeout(30);
                    sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                });
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTrackingWithIdentityResolution);
        });

        return services;
    }

    private static IServiceCollection AddRedis(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));

        var connectionString = configuration[$"{RedisOptions.SectionName}:ConnectionString"]
            ?? throw new InvalidOperationException("Redis:ConnectionString is required.");

        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(connectionString));

        services.AddSingleton<ICacheService, RedisCacheService>();

        return services;
    }

    private static IServiceCollection AddAzureBlob(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BlobStorageOptions>(configuration.GetSection(BlobStorageOptions.SectionName));

        var connectionString = configuration[$"{BlobStorageOptions.SectionName}:ConnectionString"]
            ?? throw new InvalidOperationException("Azure:BlobStorage:ConnectionString is required.");

        services.AddSingleton(new BlobServiceClient(connectionString));
        services.AddScoped<IStorageService, AzureBlobStorageService>();

        return services;
    }

    private static IServiceCollection AddAzureServiceBus(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ServiceBusOptions>(configuration.GetSection(ServiceBusOptions.SectionName));

        var connectionString = configuration[$"{ServiceBusOptions.SectionName}:ConnectionString"]
            ?? throw new InvalidOperationException("Azure:ServiceBus:ConnectionString is required.");

        services.AddSingleton(new ServiceBusClient(connectionString));
        services.AddScoped<IMessagePublisher, ServiceBusPublisher>();

        return services;
    }

    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        return services;
    }

    private static IServiceCollection AddCqrs(this IServiceCollection services)
    {
        // Dispatchers
        services.AddScoped<ICommandDispatcher, CommandDispatcher>();
        services.AddScoped<IQueryDispatcher, QueryDispatcher>();

        // Command handlers — registered against their strongly-typed interface
        services.AddScoped<ICommandHandler<CreateUploadIntentCommand, UploadIntentResponse>,
            CreateUploadIntentCommandHandler>();
        services.AddScoped<ICommandHandler<CompleteUploadCommand, DocumentId>,
            CompleteUploadCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateDocumentStatusCommand>,
            UpdateDocumentStatusCommandHandler>();

        // Query handlers
        services.AddScoped<IQueryHandler<GetDocumentStatusQuery, DocumentStatusDto>,
            GetDocumentStatusQueryHandler>();
        services.AddScoped<IQueryHandler<GetPageUrlQuery, PageUrlDto>,
            GetPageUrlQueryHandler>();
        services.AddScoped<IQueryHandler<GetDocumentManifestQuery, DocumentManifestDto>,
            GetDocumentManifestQueryHandler>();

        return services;
    }
}
