using DocumentProcessing.Core.Domain;
using DocumentProcessing.Core.Exceptions;
using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace DocumentProcessing.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IDocumentRepository"/>.</summary>
internal sealed class DocumentRepository : IDocumentRepository
{
    private readonly ApplicationDbContext _db;
    private static readonly ILogger Logger = Log.ForContext<DocumentRepository>();

    public DocumentRepository(ApplicationDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task<Document?> GetByIdAsync(DocumentId id, CancellationToken cancellationToken = default)
    {
        Logger.Debug("Fetching document {DocumentId}", id);
        return await CompiledQueries.GetDocumentById(_db, id.Value);
    }

    /// <inheritdoc/>
    public async Task<Document?> GetByIdAndTenantAsync(DocumentId id, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        Logger.Debug("Fetching document {DocumentId} for tenant {TenantId}", id, tenantId);
        return await CompiledQueries.GetDocumentByIdAndTenant(_db, id.Value, tenantId.Value);
    }

    /// <inheritdoc/>
    public async Task AddAsync(Document document, CancellationToken cancellationToken = default)
    {
        _db.Documents.Add(document);
        await _db.SaveChangesAsync(cancellationToken);
        Logger.Information("Document {DocumentId} persisted", document.Id);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(Document document, CancellationToken cancellationToken = default)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            Logger.Information("Document {DocumentId} updated to status {Status}", document.Id, document.Status);
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
        Logger.Information("Persisted {PageCount} pages", pageList.Count);
    }
}
