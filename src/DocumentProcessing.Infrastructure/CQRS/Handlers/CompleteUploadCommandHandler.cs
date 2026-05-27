using System.Diagnostics;
using DocumentProcessing.Contracts.Events;
using DocumentProcessing.Core.Commands;
using DocumentProcessing.Core.CQRS;
using DocumentProcessing.Core.Exceptions;
using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Core.ValueObjects;
using DocumentProcessing.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentProcessing.Infrastructure.CQRS.Handlers;

/// <summary>
/// Handles <see cref="CompleteUploadCommand"/>: validates the checksum, transitions the document to
/// Uploaded, and emits <see cref="DocumentUploadedEvent"/> on the processing queue.
/// </summary>
public class CompleteUploadCommandHandler : ICommandHandler<CompleteUploadCommand, DocumentId>
{
    private readonly IDocumentRepository _repository;
    private readonly IMessagePublisher _publisher;
    private readonly ServiceBusOptions _sbOptions;
    private readonly ILogger<CompleteUploadCommandHandler> _logger;
    private static readonly ActivitySource ActivitySource = new("DocumentProcessing.Infrastructure");

    public CompleteUploadCommandHandler(
        IDocumentRepository repository,
        IMessagePublisher publisher,
        IOptions<ServiceBusOptions> sbOptions,
        ILogger<CompleteUploadCommandHandler> logger)
    {
        _repository = repository;
        _publisher = publisher;
        _sbOptions = sbOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<DocumentId> HandleAsync(CompleteUploadCommand command, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("CompleteUpload");
        activity?.SetTag("upload.id", command.UploadId.ToString());

        _logger.LogInformation("Completing upload for {UploadId}", command.UploadId);

        var documentId = new DocumentId(command.UploadId);
        var document = await _repository.GetByIdAsync(documentId, cancellationToken)
            ?? throw new DocumentNotFoundException(documentId);

        var storedChecksum = Checksum.From(document.Checksum);
        var providedChecksum = Checksum.From(command.Checksum);

        if (storedChecksum != providedChecksum)
            throw new DocumentDomainException(
                $"Checksum mismatch for document {documentId}. Expected {storedChecksum}, got {providedChecksum}.");

        document.MarkUploaded(command.CorrelationId);
        await _repository.UpdateAsync(document, cancellationToken);

        var uploadedEvent = new DocumentUploadedEvent
        {
            DocumentId = document.Id,
            TenantId = document.TenantId,
            BlobPath = document.BlobPath,
            FileName = document.FileName,
            Checksum = document.Checksum,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = command.CorrelationId
        };

        await _publisher.PublishAsync(
            _sbOptions.ProcessingQueueName, uploadedEvent, command.CorrelationId, cancellationToken);

        _logger.LogInformation("Upload completed and event published for document {DocumentId}", documentId);

        activity?.SetTag("document.id", documentId.Value.ToString());

        return documentId;
    }
}
