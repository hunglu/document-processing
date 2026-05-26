using DocumentProcessing.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace DocumentProcessing.Infrastructure.Persistence;

/// <summary>EF Core compiled queries for the hot paths — eliminates query-tree compilation overhead per call.</summary>
internal static class CompiledQueries
{
    /// <summary>Compiled query: fetch a document by its Guid identifier.</summary>
    public static readonly Func<ApplicationDbContext, Guid, Task<Document?>> GetDocumentById =
        EF.CompileAsyncQuery((ApplicationDbContext ctx, Guid id) =>
            ctx.Documents
               .Include(d => d.Pages)
               .FirstOrDefault(d => d.Id == id));

    /// <summary>Compiled query: fetch a document by identifier scoped to a specific tenant.</summary>
    public static readonly Func<ApplicationDbContext, Guid, string, Task<Document?>> GetDocumentByIdAndTenant =
        EF.CompileAsyncQuery((ApplicationDbContext ctx, Guid id, string tenantId) =>
            ctx.Documents
               .Include(d => d.Pages)
               .FirstOrDefault(d => d.Id == id && d.TenantId == tenantId));

    /// <summary>Compiled query: fetch pages for a document ordered by page number.</summary>
    public static readonly Func<ApplicationDbContext, Guid, IAsyncEnumerable<DocumentPage>> GetPagesByDocumentId =
        EF.CompileAsyncQuery((ApplicationDbContext ctx, Guid documentId) =>
            ctx.DocumentPages
               .Where(p => p.DocumentId == documentId)
               .OrderBy(p => p.PageNumber));
}
