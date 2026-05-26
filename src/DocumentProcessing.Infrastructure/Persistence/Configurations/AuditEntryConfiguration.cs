using DocumentProcessing.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocumentProcessing.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="AuditEntry"/>.</summary>
internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .UseIdentityColumn();

        builder.Property(a => a.DocumentId)
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        builder.Property(a => a.FromStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(a => a.ToStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(a => a.Message)
            .HasMaxLength(2048);

        builder.Property(a => a.Actor)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(a => a.CorrelationId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(a => a.OccurredAt)
            .IsRequired();

        builder.HasIndex(a => a.DocumentId);
        builder.HasIndex(a => a.OccurredAt);
    }
}
