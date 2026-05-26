using DocumentProcessing.Core.Domain;
using DocumentProcessing.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DocumentProcessing.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IAuditRepository"/>.</summary>
internal sealed class AuditRepository : IAuditRepository
{
    private readonly ApplicationDbContext _db;

    public AuditRepository(ApplicationDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AuditEntry>> GetByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        return await _db.AuditEntries
            .Where(a => a.DocumentId == documentId)
            .OrderBy(a => a.OccurredAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        _db.AuditEntries.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
