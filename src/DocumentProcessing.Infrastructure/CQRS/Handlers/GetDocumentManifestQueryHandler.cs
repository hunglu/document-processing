using System.Diagnostics;
using DocumentProcessing.Contracts.DTOs;
using DocumentProcessing.Core.CQRS;
using DocumentProcessing.Core.Exceptions;
using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Core.Queries;
using DocumentProcessing.Core.ValueObjects;
using Serilog;

namespace DocumentProcessing.Infrastructure.CQRS.Handlers;

/// <summary>Handles <see cref="GetDocumentManifestQuery"/>: returns the full page manifest.</summary>
internal sealed class GetDocumentManifestQueryHandler : IQueryHandler<GetDocumentManifestQuery, DocumentManifestDto>
{
    private readonly IDocumentRepository _repository;
    private readonly IStorageService _storage;
    private static readonly ILogger Logger = Log.ForContext<GetDocumentManifestQueryHandler>();
    private static readonly ActivitySource ActivitySource = new("DocumentProcessing.Infrastructure");

    public GetDocumentManifestQueryHandler(IDocumentRepository repository, IStorageService storage)
    {
        _repository = repository;
        _storage = storage;
    }

    /// <inheritdoc/>
    public async Task<DocumentManifestDto> HandleAsync(GetDocumentManifestQuery query, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("GetDocumentManifest");
        activity?.SetTag("document.id", query.DocumentId.ToString());

        Logger.Debug("Getting manifest for document {DocumentId}", query.DocumentId);

        var document = await _repository.GetByIdAndTenantAsync(
            new DocumentId(query.DocumentId), TenantId.From(query.TenantId), cancellationToken)
            ?? throw new DocumentNotFoundException(new DocumentId(query.DocumentId));

        var pageUrlTasks = document.Pages
            .OrderBy(p => p.PageNumber)
            .Select(async p =>
            {
                var (fullUrl, thumbUrl, expiresAt) = await _storage.GetPageCdnUrlsAsync(
                    p.FullBlobPath, p.ThumbnailBlobPath, cancellationToken);

                return new PageUrlDto
                {
                    PageNumber = p.PageNumber,
                    FullUrl = fullUrl,
                    ThumbnailUrl = thumbUrl,
                    ExpiresAt = expiresAt
                };
            });

        var pages = await Task.WhenAll(pageUrlTasks);

        Logger.Information("Manifest built for document {DocumentId} with {PageCount} pages", query.DocumentId, pages.Length);

        return new DocumentManifestDto
        {
            DocumentId = document.Id,
            FileName = document.FileName,
            PageCount = document.PageCount ?? 0,
            Status = document.Status,
            CreatedAt = document.CreatedAt,
            Pages = pages
        };
    }
}
