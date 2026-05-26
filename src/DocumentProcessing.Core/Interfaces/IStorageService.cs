namespace DocumentProcessing.Core.Interfaces;

/// <summary>Abstraction over Azure Blob Storage for document and page assets.</summary>
public interface IStorageService
{
    /// <summary>
    /// Generates a pre-signed SAS URL that the client can PUT bytes to directly.
    /// Returns the SAS URL and the blob path used as a key for subsequent operations.
    /// </summary>
    Task<(string SasUrl, string BlobPath, DateTimeOffset ExpiresAt)> GenerateSasUploadUrlAsync(
        string tenantId,
        string documentId,
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>Downloads the original PDF blob as a stream; caller is responsible for disposal.</summary>
    Task<Stream> DownloadOriginalAsync(string blobPath, CancellationToken cancellationToken = default);

    /// <summary>Uploads a rendered page WebP image and returns the blob path.</summary>
    Task<string> UploadPageAsync(
        string documentId,
        int pageNumber,
        bool isThumbnail,
        Stream content,
        CancellationToken cancellationToken = default);

    /// <summary>Generates a short-lived CDN-signed URL for a page blob path.</summary>
    Task<(string FullUrl, string ThumbnailUrl, DateTimeOffset ExpiresAt)> GetPageCdnUrlsAsync(
        string fullBlobPath,
        string thumbnailBlobPath,
        CancellationToken cancellationToken = default);
}
