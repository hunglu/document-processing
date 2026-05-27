using System.Diagnostics;
using DocumentProcessing.Contracts.DTOs;
using DocumentProcessing.Core.CQRS;
using DocumentProcessing.Core.Exceptions;
using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Core.Queries;
using DocumentProcessing.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace DocumentProcessing.Infrastructure.CQRS.Handlers;

/// <summary>Handles <see cref="GetDocumentManifestQuery"/>: returns the full page manifest with extracted text.</summary>
public class GetDocumentManifestQueryHandler : IQueryHandler<GetDocumentManifestQuery, DocumentManifestDto>
{
    private readonly IDocumentRepository _repository;
    private readonly ILogger<GetDocumentManifestQueryHandler> _logger;
    private static readonly ActivitySource ActivitySource = new("DocumentProcessing.Infrastructure");

    public GetDocumentManifestQueryHandler(
        IDocumentRepository repository,
        ILogger<GetDocumentManifestQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<DocumentManifestDto> HandleAsync(GetDocumentManifestQuery query, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("GetDocumentManifest");
        activity?.SetTag("document.id", query.DocumentId.ToString());

        _logger.LogDebug("Getting manifest for document {DocumentId}", query.DocumentId);

        var document = await _repository.GetByIdAndTenantAsync(
            new DocumentId(query.DocumentId), TenantId.From(query.TenantId), cancellationToken)
            ?? throw new DocumentNotFoundException(new DocumentId(query.DocumentId));

        var pages = document.Pages
            .OrderBy(p => p.PageNumber)
            .Select(p => new PageUrlDto
            {
                PageNumber = p.PageNumber,
                ExtractedText = p.ExtractedText
            })
            .ToArray();

        _logger.LogInformation("Manifest built for document {DocumentId} with {PageCount} pages",
            query.DocumentId, pages.Length);

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
