using DocumentProcessing.Core.ValueObjects;

namespace DocumentProcessing.Core.Domain;

/// <summary>Represents a single rendered page belonging to a <see cref="Document"/>.</summary>
public sealed class DocumentPage
{
    private DocumentPage() { }

    /// <summary>Surrogate primary key.</summary>
    public long Id { get; private set; }

    /// <summary>Foreign key to the owning document.</summary>
    public Guid DocumentId { get; private set; }

    /// <summary>1-based page number.</summary>
    public int PageNumber { get; private set; }

    /// <summary>Blob path of the full-resolution WebP image.</summary>
    public string FullBlobPath { get; private set; } = string.Empty;

    /// <summary>Blob path of the thumbnail WebP image.</summary>
    public string ThumbnailBlobPath { get; private set; } = string.Empty;

    /// <summary>Width of the rendered page in pixels.</summary>
    public int WidthPx { get; private set; }

    /// <summary>Height of the rendered page in pixels.</summary>
    public int HeightPx { get; private set; }

    /// <summary>UTC timestamp when this page record was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Factory method used by the worker after successfully uploading rendered WebP images.</summary>
    public static DocumentPage Create(
        Guid documentId,
        PageNumber pageNumber,
        string fullBlobPath,
        string thumbnailBlobPath,
        int widthPx,
        int heightPx)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullBlobPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(thumbnailBlobPath);
        if (widthPx <= 0) throw new ArgumentOutOfRangeException(nameof(widthPx));
        if (heightPx <= 0) throw new ArgumentOutOfRangeException(nameof(heightPx));

        return new DocumentPage
        {
            DocumentId = documentId,
            PageNumber = pageNumber.Value,
            FullBlobPath = fullBlobPath,
            ThumbnailBlobPath = thumbnailBlobPath,
            WidthPx = widthPx,
            HeightPx = heightPx,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
