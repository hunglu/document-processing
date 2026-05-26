using DocumentProcessing.Contracts.Enums;
using DocumentProcessing.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocumentProcessing.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for the <see cref="Document"/> aggregate root.</summary>
internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnType("uniqueidentifier")
            .ValueGeneratedNever();

        builder.Property(d => d.TenantId)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(d => d.FileName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(d => d.BlobPath)
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(d => d.Checksum)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(d => d.FileSizeBytes)
            .IsRequired();

        builder.Property(d => d.PageCount);

        builder.Property(d => d.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(d => d.FailureReason)
            .HasConversion<string>()
            .HasMaxLength(64);

        builder.Property(d => d.FailureMessage)
            .HasMaxLength(2048);

        builder.Property(d => d.CreatedAt)
            .IsRequired();

        builder.Property(d => d.UpdatedAt)
            .IsRequired()
            .IsConcurrencyToken();

        // Optimistic concurrency via row version
        builder.Property(d => d.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasIndex(d => d.TenantId);
        builder.HasIndex(d => new { d.TenantId, d.Status });
        builder.HasIndex(d => d.CreatedAt);

        builder.HasMany(d => d.Pages)
            .WithOne()
            .HasForeignKey(p => p.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(d => d.AuditEntries)
            .WithOne()
            .HasForeignKey(a => a.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Map private backing fields
        builder.Navigation(d => d.Pages).HasField("_pages");
        builder.Navigation(d => d.AuditEntries).HasField("_auditEntries");
    }
}
