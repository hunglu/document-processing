using DocumentProcessing.Core.Domain;

namespace DocumentProcessing.Core.Interfaces;

/// <summary>Repository contract for the append-only audit trail.</summary>
public interface IAuditRepository
{
    /// <summary>Returns all audit entries for the given document ordered by <see cref="AuditEntry.OccurredAt"/> ascending.</summary>
    Task<IReadOnlyList<AuditEntry>> GetByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>Appends a new audit entry; update operations are not permitted.</summary>
    Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
