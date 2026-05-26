namespace DocumentProcessing.Infrastructure.Configuration;

/// <summary>Typed options for Azure Blob Storage configuration.</summary>
public sealed class BlobStorageOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Azure:BlobStorage";

    /// <summary>Storage account connection string or managed-identity endpoint.</summary>
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>Container for original uploaded PDFs.</summary>
    public string DocsContainer { get; init; } = "docs";

    /// <summary>Container for rendered page WebP images.</summary>
    public string PagesContainer { get; init; } = "pages";

    /// <summary>Minutes until a SAS upload URL expires.</summary>
    public int SasTokenExpiryMinutes { get; init; } = 60;

    /// <summary>Minutes until a CDN page URL token expires.</summary>
    public int CdnTokenExpiryMinutes { get; init; } = 60;

    /// <summary>Azure CDN endpoint base URL (e.g. https://mydocs.azureedge.net).</summary>
    public string CdnEndpointBaseUrl { get; init; } = string.Empty;
}
