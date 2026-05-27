using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentProcessing.Infrastructure.Storage;

/// <summary>Azure Blob Storage implementation of <see cref="IStorageService"/>.</summary>
internal sealed class AzureBlobStorageService : IStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly BlobStorageOptions _options;
    private readonly ILogger<AzureBlobStorageService> _logger;

    public AzureBlobStorageService(
        BlobServiceClient blobServiceClient,
        IOptions<BlobStorageOptions> options,
        ILogger<AzureBlobStorageService> logger)
    {
        _blobServiceClient = blobServiceClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<(string SasUrl, string BlobPath, DateTimeOffset ExpiresAt)> GenerateSasUploadUrlAsync(
        string tenantId, string documentId, string fileName, CancellationToken cancellationToken = default)
    {
        var container = _blobServiceClient.GetBlobContainerClient(_options.DocsContainer);
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var blobPath = $"{tenantId}/{documentId}/original/{fileName}";
        var blobClient = container.GetBlobClient(blobPath);

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.SasTokenExpiryMinutes);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _options.DocsContainer,
            BlobName = blobPath,
            Resource = "b",
            ExpiresOn = expiresAt
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Write | BlobSasPermissions.Create);

        var sasUri = blobClient.GenerateSasUri(sasBuilder);

        _logger.LogInformation("Generated SAS upload URL for document {DocumentId}, expires {ExpiresAt}",
            documentId, expiresAt);

        return (sasUri.ToString(), blobPath, expiresAt);
    }

    /// <inheritdoc/>
    public async Task<Stream> DownloadOriginalAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        var container = _blobServiceClient.GetBlobContainerClient(_options.DocsContainer);
        var blobClient = container.GetBlobClient(blobPath);

        _logger.LogDebug("Downloading original PDF from blob {BlobPath}", blobPath);

        var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return response.Value.Content;
    }
}
