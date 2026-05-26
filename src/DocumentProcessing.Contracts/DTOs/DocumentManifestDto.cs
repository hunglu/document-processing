using DocumentProcessing.Contracts.Enums;

namespace DocumentProcessing.Contracts.DTOs;

/// <summary>Complete manifest of a processed document including all page URLs.</summary>
public sealed class DocumentManifestDto
{
    /// <summary>Unique document identifier.</summary>
    public Guid DocumentId { get; init; }

    /// <summary>Original file name as supplied by the uploader.</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>Total number of pages.</summary>
    public int PageCount { get; init; }

    /// <summary>Current document status.</summary>
    public DocumentStatus Status { get; init; }

    /// <summary>UTC creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Ordered list of page URLs (1-based).</summary>
    public IReadOnlyList<PageUrlDto> Pages { get; init; } = [];
}
