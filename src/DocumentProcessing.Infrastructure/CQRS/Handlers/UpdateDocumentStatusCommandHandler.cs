using System.Diagnostics;
using DocumentProcessing.Contracts.Enums;
using DocumentProcessing.Core.Commands;
using DocumentProcessing.Core.CQRS;
using DocumentProcessing.Core.Exceptions;
using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Core.ValueObjects;
using Serilog;

namespace DocumentProcessing.Infrastructure.CQRS.Handlers;

/// <summary>Handles <see cref="UpdateDocumentStatusCommand"/>: applies the status transition on the domain aggregate.</summary>
internal sealed class UpdateDocumentStatusCommandHandler : ICommandHandler<UpdateDocumentStatusCommand>
{
    private readonly IDocumentRepository _repository;
    private static readonly ILogger Logger = Log.ForContext<UpdateDocumentStatusCommandHandler>();
    private static readonly ActivitySource ActivitySource = new("DocumentProcessing.Infrastructure");

    public UpdateDocumentStatusCommandHandler(IDocumentRepository repository)
        => _repository = repository;

    /// <inheritdoc/>
    public async Task HandleAsync(UpdateDocumentStatusCommand command, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("UpdateDocumentStatus");
        activity?.SetTag("document.id", command.DocumentId.ToString());
        activity?.SetTag("new.status", command.NewStatus.ToString());

        Logger.Information("Updating document {DocumentId} to status {NewStatus}", command.DocumentId, command.NewStatus);

        var documentId = new DocumentId(command.DocumentId);
        var document = await _repository.GetByIdAsync(documentId, cancellationToken)
            ?? throw new DocumentNotFoundException(documentId);

        switch (command.NewStatus)
        {
            case DocumentStatus.Processing:
                document.MarkProcessing(command.CorrelationId);
                break;

            case DocumentStatus.Ready:
                if (!command.PageCount.HasValue)
                    throw new ArgumentException("PageCount is required when transitioning to Ready.");
                document.MarkReady(command.PageCount.Value, command.CorrelationId);
                break;

            case DocumentStatus.Failed:
                document.MarkFailed(
                    command.FailureReason ?? ProcessingFailureReason.Unknown,
                    command.FailureMessage ?? "Unknown error",
                    command.CorrelationId);
                break;

            default:
                throw new ArgumentException($"Status {command.NewStatus} cannot be set via this command.");
        }

        await _repository.UpdateAsync(document, cancellationToken);

        Logger.Information("Document {DocumentId} transitioned to {NewStatus}", command.DocumentId, command.NewStatus);
    }
}
