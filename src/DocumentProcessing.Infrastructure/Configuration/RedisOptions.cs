namespace DocumentProcessing.Infrastructure.Configuration;

/// <summary>Typed options for Redis configuration.</summary>
public sealed class RedisOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Redis";

    /// <summary>StackExchange.Redis connection string.</summary>
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>Default cache TTL in minutes when no TTL is specified.</summary>
    public int DefaultTtlMinutes { get; init; } = 60;

    /// <summary>TTL for page URL cache entries in minutes (should match CDN token expiry).</summary>
    public int PageUrlTtlMinutes { get; init; } = 55;
}
