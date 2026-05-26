using DocumentProcessing.Core.ValueObjects;

namespace DocumentProcessing.Core.Exceptions;

/// <summary>Thrown when an optimistic concurrency conflict is detected during a document update.</summary>
public sealed class DocumentConflictException : Exception
{
    /// <summary>The document identifier involved in the conflict.</summary>
    public DocumentId DocumentId { get; }

    /// <inheritdoc/>
    public DocumentConflictException(DocumentId documentId)
        : base($"Concurrency conflict on document '{documentId}'. Please retry.")
    {
        DocumentId = documentId;
    }
}
