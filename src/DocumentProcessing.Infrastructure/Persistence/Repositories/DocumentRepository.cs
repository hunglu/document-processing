using DocumentProcessing.Core.Domain;
using DocumentProcessing.Core.Exceptions;
using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DocumentProcessing.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IDocumentRepository"/>.</summary>
internal sealed class DocumentRepository : IDocumentRepository
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<DocumentRepository> _logger;

    public DocumentRepository(ApplicationDbContext db, ILogger<DocumentRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Document?> GetByIdAsync(DocumentId id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching document {DocumentId}", id);
        return await CompiledQueries.GetDocumentById(_db, id.Value);
    }

    /// <inheritdoc/>
    public async Task<Document?> GetByIdAndTenantAsync(DocumentId id, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching document {DocumentId} for tenant {TenantId}", id, tenantId);
        return await CompiledQueries.GetDocumentByIdAndTenant(_db, id.Value, tenantId.Value);
    }

    /// <inheritdoc/>
    public async Task AddAsync(Document document, CancellationToken cancellationToken = default)
    {
        _db.Documents.Add(document);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Document {DocumentId} persisted", document.Id);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(Document document, CancellationToken cancellationToken = default)
    {
        try
        {
            // DbContext uses NoTrackingWithIdentityResolution so entities from queries are
            // untracked. Explicitly attach and mark modified so EF Core generates an UPDATE.
            if (_db.Entry(document).State == EntityState.Detached)
                _db.Documents.Update(document);

            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Document {DocumentId} updated to status {Status}", document.Id, document.Status);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new DocumentConflictException(new DocumentId(document.Id));
        }
    }

    /// <inheritdoc/>
    public async Task AddPagesAsync(IEnumerable<DocumentPage> pages, CancellationToken cancellationToken = default)
    {
        var pageList = pages.ToList();
        _db.DocumentPages.AddRange(pageList);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Persisted {PageCount} pages", pageList.Count);
    }
}
