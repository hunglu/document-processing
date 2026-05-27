using DocumentProcessing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using Testcontainers.Azurite;
using Testcontainers.MsSql;
using Testcontainers.Redis;
using Xunit;

namespace DocumentProcessing.IntegrationTests.Fixtures;

/// <summary>
/// Shared WebApplicationFactory that spins up Testcontainers for SQL Server, Redis, and Azurite.
/// Implements IAsyncLifetime so containers are started/stopped around the test collection.
/// </summary>
public sealed class IntegrationTestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("Integration_Test_Pass123!")
        .Build();

    private readonly RedisContainer _redisContainer = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    private readonly AzuriteContainer _azuriteContainer = new AzuriteBuilder()
        .WithImage("mcr.microsoft.com/azure-storage/azurite")
        .Build();

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Override SQL Server connection
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(_sqlContainer.GetConnectionString()));

            // Override Redis connection
            services.RemoveAll<StackExchange.Redis.IConnectionMultiplexer>();
            services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(
                StackExchange.Redis.ConnectionMultiplexer.Connect(_redisContainer.GetConnectionString()));

            // Override Blob Storage (Azurite)
            services.RemoveAll<Azure.Storage.Blobs.BlobServiceClient>();
            services.AddSingleton(new Azure.Storage.Blobs.BlobServiceClient(
                _azuriteContainer.GetConnectionString()));
        });
    }

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _sqlContainer.StartAsync(),
            _redisContainer.StartAsync(),
            _azuriteContainer.StartAsync());

        // Apply migrations
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
    }

    /// <inheritdoc/>
    public new async Task DisposeAsync()
    {
        await _sqlContainer.DisposeAsync();
        await _redisContainer.DisposeAsync();
        await _azuriteContainer.DisposeAsync();
        await base.DisposeAsync();
    }
}
