using System.Diagnostics;
using DocumentProcessing.Contracts.DTOs;
using DocumentProcessing.Core.Commands;
using DocumentProcessing.Core.CQRS;
using DocumentProcessing.Core.Domain;
using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Core.ValueObjects;
using Serilog;

namespace DocumentProcessing.Infrastructure.CQRS.Handlers;

/// <summary>Handles <see cref="CreateUploadIntentCommand"/>: creates a document record and issues a SAS URL.</summary>
public class CreateUploadIntentCommandHandler : ICommandHandler<CreateUploadIntentCommand, UploadIntentResponse>
{
    private readonly IDocumentRepository _repository;
    private readonly IStorageService _storage;
    private static readonly ILogger Logger = Log.ForContext<CreateUploadIntentCommandHandler>();
    private static readonly ActivitySource ActivitySource = new("DocumentProcessing.Infrastructure");

    public CreateUploadIntentCommandHandler(IDocumentRepository repository, IStorageService storage)
    {
        _repository = repository;
        _storage = storage;
    }

    /// <inheritdoc/>
    public async Task<UploadIntentResponse> HandleAsync(CreateUploadIntentCommand command, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("CreateUploadIntent");

        var documentId = DocumentId.New();
        var tenantId = TenantId.From(command.TenantId);
        var checksum = Checksum.From(command.Checksum);

        activity?.SetTag("document.id", documentId.Value.ToString());
        activity?.SetTag("tenant.id", tenantId.Value);

        Logger.Information("Creating upload intent for {FileName}, tenant {TenantId}, documentId {DocumentId}",
            command.FileName, command.TenantId, documentId);

        var (sasUrl, blobPath, expiresAt) = await _storage.GenerateSasUploadUrlAsync(
            tenantId.Value, documentId.Value.ToString(), command.FileName, cancellationToken);

        var document = Document.CreateUploadIntent(
            documentId, tenantId, command.FileName, blobPath, checksum, command.FileSizeBytes, command.CorrelationId);

        await _repository.AddAsync(document, cancellationToken);

        Logger.Information("Upload intent created for document {DocumentId}, SAS expires {ExpiresAt}",
            documentId, expiresAt);

        return new UploadIntentResponse
        {
            UploadId = documentId.Value,
            SasUploadUrl = sasUrl,
            SasExpiresAt = expiresAt
        };
    }
}
