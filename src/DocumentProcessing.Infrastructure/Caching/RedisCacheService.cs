using System.Text.Json;
using DocumentProcessing.Contracts.DTOs;
using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace DocumentProcessing.Infrastructure.Caching;

/// <summary>StackExchange.Redis implementation of <see cref="ICacheService"/>.</summary>
public class RedisCacheService : ICacheService
{
    private readonly IDatabase _db;
    private readonly RedisOptions _options;
    private readonly ILogger<RedisCacheService> _logger;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public RedisCacheService(
        IConnectionMultiplexer redis,
        IOptions<RedisOptions> options,
        ILogger<RedisCacheService> logger)
    {
        _db = redis.GetDatabase();
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        var value = await _db.StringGetAsync(key);

        if (!value.HasValue)
        {
            _logger.LogDebug("Cache miss for key {CacheKey}", key);
            return null;
        }

        _logger.LogDebug("Cache hit for key {CacheKey}", key);
        return JsonSerializer.Deserialize<T>(value.ToString(), SerializerOptions);
    }

    /// <inheritdoc/>
    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class
    {
        var json = JsonSerializer.Serialize(value, SerializerOptions);
        await _db.StringSetAsync(key, json, ttl);
        _logger.LogDebug("Cache set for key {CacheKey}, TTL {Ttl}", key, ttl);
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await _db.KeyDeleteAsync(key);
        _logger.LogDebug("Cache key deleted {CacheKey}", key);
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
