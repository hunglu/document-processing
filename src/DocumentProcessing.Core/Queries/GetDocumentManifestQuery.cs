using DocumentProcessing.Contracts.DTOs;
using DocumentProcessing.Core.CQRS;

namespace DocumentProcessing.Core.Queries;

/// <summary>Returns the full manifest of a document including all page URLs.</summary>
public sealed class GetDocumentManifestQuery : IQuery<DocumentManifestDto>
{
    /// <summary>Document to retrieve.</summary>
    public Guid DocumentId { get; init; }

    /// <summary>Calling tenant — enforces row-level security.</summary>
    public string TenantId { get; init; } = string.Empty;
}
