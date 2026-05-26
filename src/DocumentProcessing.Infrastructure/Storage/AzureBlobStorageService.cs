using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Serilog;

namespace DocumentProcessing.Infrastructure.Storage;

/// <summary>Azure Blob Storage implementation of <see cref="IStorageService"/>.</summary>
internal sealed class AzureBlobStorageService : IStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly BlobStorageOptions _options;
    private static readonly ILogger Logger = Log.ForContext<AzureBlobStorageService>();

    public AzureBlobStorageService(BlobServiceClient blobServiceClient, IOptions<BlobStorageOptions> options)
    {
        _blobServiceClient = blobServiceClient;
        _options = options.Value;
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

        Logger.Information("Generated SAS upload URL for document {DocumentId}, expires {ExpiresAt}", documentId, expiresAt);

        return (sasUri.ToString(), blobPath, expiresAt);
    }

    /// <inheritdoc/>
    public async Task<Stream> DownloadOriginalAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        var container = _blobServiceClient.GetBlobContainerClient(_options.DocsContainer);
        var blobClient = container.GetBlobClient(blobPath);

        Logger.Debug("Downloading original PDF from blob {BlobPath}", blobPath);

        var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return response.Value.Content;
    }

    /// <inheritdoc/>
    public async Task<string> UploadPageAsync(
        string documentId, int pageNumber, bool isThumbnail, Stream content, CancellationToken cancellationToken = default)
    {
        var container = _blobServiceClient.GetBlobContainerClient(_options.PagesContainer);
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var suffix = isThumbnail ? "thumb" : "full";
        var blobPath = $"{documentId}/page-{pageNumber:D4}-{suffix}.webp";
        var blobClient = container.GetBlobClient(blobPath);

        var headers = new BlobHttpHeaders { ContentType = "image/webp" };

        await blobClient.UploadAsync(content, new BlobUploadOptions { HttpHeaders = headers }, cancellationToken);

        Logger.Debug("Uploaded page {PageNumber} ({Suffix}) for document {DocumentId}", pageNumber, suffix, documentId);

        return blobPath;
    }

    /// <inheritdoc/>
    public Task<(string FullUrl, string ThumbnailUrl, DateTimeOffset ExpiresAt)> GetPageCdnUrlsAsync(
        string fullBlobPath, string thumbnailBlobPath, CancellationToken cancellationToken = default)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.CdnTokenExpiryMinutes);

        // CDN signed URLs are constructed using the CDN endpoint base URL
        var fullUrl = $"{_options.CdnEndpointBaseUrl}/{_options.PagesContainer}/{fullBlobPath}";
        var thumbUrl = $"{_options.CdnEndpointBaseUrl}/{_options.PagesContainer}/{thumbnailBlobPath}";

        return Task.FromResult((fullUrl, thumbUrl, expiresAt));
    }
}
