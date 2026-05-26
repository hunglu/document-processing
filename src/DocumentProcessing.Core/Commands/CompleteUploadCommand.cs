using DocumentProcessing.Core.CQRS;
using DocumentProcessing.Core.ValueObjects;

namespace DocumentProcessing.Core.Commands;

/// <summary>
/// Confirms that the client has finished uploading bytes to the SAS URL.
/// Transitions the document to Uploaded and emits <see cref="Contracts.Events.DocumentUploadedEvent"/>.
/// </summary>
public sealed class CompleteUploadCommand : ICommand<DocumentId>
{
    /// <summary>Upload intent identifier returned from <see cref="CreateUploadIntentCommand"/>.</summary>
    public Guid UploadId { get; init; }

    /// <summary>SHA-256 checksum the client computed — must match the stored intent checksum.</summary>
    public string Checksum { get; init; } = string.Empty;

    /// <summary>Correlation ID from the HTTP request.</summary>
    public string CorrelationId { get; init; } = string.Empty;
}
