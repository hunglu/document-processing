using DocumentProcessing.Contracts.Enums;
using DocumentProcessing.Core.CQRS;

namespace DocumentProcessing.Core.Commands;

/// <summary>Updates a document's lifecycle status; used internally by the worker.</summary>
public sealed class UpdateDocumentStatusCommand : ICommand
{
    /// <summary>Document to update.</summary>
    public Guid DocumentId { get; init; }

    /// <summary>Target status.</summary>
    public DocumentStatus NewStatus { get; init; }

    /// <summary>Required when <see cref="NewStatus"/> is <see cref="DocumentStatus.Ready"/>.</summary>
    public int? PageCount { get; init; }

    /// <summary>Required when <see cref="NewStatus"/> is <see cref="DocumentStatus.Failed"/>.</summary>
    public ProcessingFailureReason? FailureReason { get; init; }

    /// <summary>Optional failure message.</summary>
    public string? FailureMessage { get; init; }

    /// <summary>Correlation ID propagated from the Service Bus message.</summary>
    public string CorrelationId { get; init; } = string.Empty;
}
