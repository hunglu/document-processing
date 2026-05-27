using System.Text.Json;
using DocumentProcessing.Contracts.DTOs;
using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Serilog;
using StackExchange.Redis;

namespace DocumentProcessing.Infrastructure.Caching;

/// <summary>StackExchange.Redis implementation of <see cref="ICacheService"/>.</summary>
public class RedisCacheService : ICacheService
{
    private readonly IDatabase _db;
    private readonly RedisOptions _options;
    private static readonly ILogger Logger = Log.ForContext<RedisCacheService>();

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public RedisCacheService(IConnectionMultiplexer redis, IOptions<RedisOptions> options)
    {
        _db = redis.GetDatabase();
        _options = options.Value;
    }

    /// <inheritdoc/>
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        var value = await _db.StringGetAsync(key);

        if (!value.HasValue)
        {
            Logger.Debug("Cache miss for key {CacheKey}", key);
            return null;
        }

        Logger.Debug("Cache hit for key {CacheKey}", key);
        return JsonSerializer.Deserialize<T>(value.ToString(), SerializerOptions);
    }

    /// <inheritdoc/>
    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class
    {
        var json = JsonSerializer.Serialize(value, SerializerOptions);
        await _db.StringSetAsync(key, json, ttl);
        Logger.Debug("Cache set for key {CacheKey}, TTL {Ttl}", key, ttl);
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await _db.KeyDeleteAsync(key);
        Logger.Debug("Cache key deleted {CacheKey}", key);
    }

    /// <inheritdoc/>
    public Task<PageUrlDto?> GetPageUrlAsync(Guid documentId, int pageNumber, CancellationToken cancellationToken = default)
        => GetAsync<PageUrlDto>(PageUrlKey(documentId, pageNumber), cancellationToken);

    /// <inheritdoc/>
    public Task SetPageUrlAsync(Guid documentId, int pageNumber, PageUrlDto dto, TimeSpan ttl, CancellationToken cancellationToken = default)
        => SetAsync(PageUrlKey(documentId, pageNumber), dto, ttl, cancellationToken);

    private static string PageUrlKey(Guid documentId, int pageNumber)
        => $"page-url:{documentId}:{pageNumber}";
}
