namespace DocumentProcessing.Core.Interfaces;

/// <summary>Abstraction over Azure Blob Storage for original document assets.</summary>
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
}
