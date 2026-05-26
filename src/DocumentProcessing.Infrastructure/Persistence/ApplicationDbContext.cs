using DocumentProcessing.Core.Domain;
using DocumentProcessing.Core.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace DocumentProcessing.Infrastructure.Persistence;

/// <summary>Primary EF Core DbContext for the document processing system.</summary>
public sealed class ApplicationDbContext : DbContext
{
    /// <inheritdoc/>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    /// <summary>Document aggregate roots.</summary>
    public DbSet<Document> Documents => Set<Document>();

    /// <summary>Rendered page records.</summary>
    public DbSet<DocumentPage> DocumentPages => Set<DocumentPage>();

    /// <summary>Append-only audit trail.</summary>
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    /// <inheritdoc/>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnforceAuditImmutability();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void EnforceAuditImmutability()
    {
        var auditUpdates = ChangeTracker.Entries<AuditEntry>()
            .Where(e => e.State is EntityState.Modified or EntityState.Deleted);

        if (auditUpdates.Any())
            throw new DocumentDomainException("AuditEntry records are append-only and cannot be modified or deleted.");
    }
}
