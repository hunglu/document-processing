using DocumentProcessing.Core.ValueObjects;

namespace DocumentProcessing.Core.Domain;

/// <summary>Represents a single extracted page belonging to a <see cref="Document"/>.</summary>
public sealed class DocumentPage
{
    private DocumentPage() { }

    /// <summary>Surrogate primary key.</summary>
    public long Id { get; private set; }

    /// <summary>Foreign key to the owning document.</summary>
    public Guid DocumentId { get; private set; }

    /// <summary>1-based page number.</summary>
    public int PageNumber { get; private set; }

    /// <summary>Plain text extracted from this page.</summary>
    public string ExtractedText { get; private set; } = string.Empty;

    /// <summary>UTC timestamp when this page record was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Factory method used by the worker after extracting text from a PDF page.</summary>
    public static DocumentPage Create(Guid documentId, PageNumber pageNumber, string extractedText)
    {
        return new DocumentPage
        {
            DocumentId = documentId,
            PageNumber = pageNumber.Value,
            ExtractedText = extractedText,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
