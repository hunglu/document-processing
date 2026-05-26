using System.Diagnostics;
using DocumentProcessing.Contracts.DTOs;
using DocumentProcessing.Core.CQRS;
using DocumentProcessing.Core.Exceptions;
using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Core.Queries;
using DocumentProcessing.Core.ValueObjects;
using Serilog;

namespace DocumentProcessing.Infrastructure.CQRS.Handlers;

/// <summary>Handles <see cref="GetDocumentStatusQuery"/>.</summary>
internal sealed class GetDocumentStatusQueryHandler : IQueryHandler<GetDocumentStatusQuery, DocumentStatusDto>
{
    private readonly IDocumentRepository _repository;
    private static readonly ILogger Logger = Log.ForContext<GetDocumentStatusQueryHandler>();
    private static readonly ActivitySource ActivitySource = new("DocumentProcessing.Infrastructure");

    public GetDocumentStatusQueryHandler(IDocumentRepository repository) => _repository = repository;

    /// <inheritdoc/>
    public async Task<DocumentStatusDto> HandleAsync(GetDocumentStatusQuery query, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("GetDocumentStatus");
        activity?.SetTag("document.id", query.DocumentId.ToString());

        Logger.Debug("Getting status for document {DocumentId}", query.DocumentId);

        var document = await _repository.GetByIdAndTenantAsync(
            new DocumentId(query.DocumentId), TenantId.From(query.TenantId), cancellationToken)
            ?? throw new DocumentNotFoundException(new DocumentId(query.DocumentId));

        return new DocumentStatusDto
        {
            DocumentId = document.Id,
            Status = document.Status,
            PageCount = document.PageCount,
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt,
            FailureReason = document.FailureReason,
            FailureMessage = document.FailureMessage
        };
    }
}
