using DocumentProcessing.Core.ValueObjects;

namespace DocumentProcessing.Core.Exceptions;

/// <summary>Thrown when a requested document does not exist or is not accessible to the caller's tenant.</summary>
public sealed class DocumentNotFoundException : Exception
{
    /// <summary>The document identifier that was not found.</summary>
    public DocumentId DocumentId { get; }

    /// <inheritdoc/>
    public DocumentNotFoundException(DocumentId documentId)
        : base($"Document '{documentId}' was not found.")
    {
        DocumentId = documentId;
    }
}
