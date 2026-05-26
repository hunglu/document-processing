using DocumentProcessing.Contracts.DTOs;
using DocumentProcessing.Core.CQRS;

namespace DocumentProcessing.Core.Queries;

/// <summary>Returns the current status of a document.</summary>
public sealed class GetDocumentStatusQuery : IQuery<DocumentStatusDto>
{
    /// <summary>Document to query.</summary>
    public Guid DocumentId { get; init; }

    /// <summary>Calling tenant — enforces row-level security.</summary>
    public string TenantId { get; init; } = string.Empty;
}
