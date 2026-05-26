using DocumentProcessing.Contracts.DTOs;

namespace DocumentProcessing.Core.Interfaces;

/// <summary>Typed Redis cache abstraction — use this instead of IDistributedCache.</summary>
public interface ICacheService
{
    /// <summary>Gets a cached value deserialised to <typeparamref name="T"/>; returns null on miss.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

    /// <summary>Sets a value in the cache with the specified TTL.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class;

    /// <summary>Removes a cache entry.</summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Gets cached page URLs for a specific document page.</summary>
    Task<PageUrlDto?> GetPageUrlAsync(Guid documentId, int pageNumber, CancellationToken cancellationToken = default);

    /// <summary>Caches page URLs for a specific document page with the standard CDN token TTL.</summary>
    Task SetPageUrlAsync(Guid documentId, int pageNumber, PageUrlDto dto, TimeSpan ttl, CancellationToken cancellationToken = default);
}
