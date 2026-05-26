using DocumentProcessing.Core.Domain;
using DocumentProcessing.Core.ValueObjects;

namespace DocumentProcessing.Core.Interfaces;

/// <summary>Repository contract for the <see cref="Document"/> aggregate.</summary>
public interface IDocumentRepository
{
    /// <summary>Returns the document by its identifier; null if not found.</summary>
    Task<Document?> GetByIdAsync(DocumentId id, CancellationToken cancellationToken = default);

    /// <summary>Returns the document by its identifier and tenant, enforcing row-level security; null if not found.</summary>
    Task<Document?> GetByIdAndTenantAsync(DocumentId id, TenantId tenantId, CancellationToken cancellationToken = default);

    /// <summary>Persists a new document.</summary>
    Task AddAsync(Document document, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing document; throws on concurrency conflict.</summary>
    Task UpdateAsync(Document document, CancellationToken cancellationToken = default);

    /// <summary>Adds a batch of <see cref="DocumentPage"/> records efficiently.</summary>
    Task AddPagesAsync(IEnumerable<DocumentPage> pages, CancellationToken cancellationToken = default);
}
