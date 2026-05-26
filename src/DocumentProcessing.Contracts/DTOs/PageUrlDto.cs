namespace DocumentProcessing.Contracts.DTOs;

/// <summary>CDN URLs for a single rendered document page.</summary>
public sealed class PageUrlDto
{
    /// <summary>1-based page number.</summary>
    public int PageNumber { get; init; }

    /// <summary>CDN URL for the full-resolution WebP image.</summary>
    public string FullUrl { get; init; } = string.Empty;

    /// <summary>CDN URL for the thumbnail WebP image.</summary>
    public string ThumbnailUrl { get; init; } = string.Empty;

    /// <summary>UTC timestamp after which the CDN URL token expires; null for public URLs.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}
