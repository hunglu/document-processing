using DocumentProcessing.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocumentProcessing.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="DocumentPage"/>.</summary>
internal sealed class DocumentPageConfiguration : IEntityTypeConfiguration<DocumentPage>
{
    public void Configure(EntityTypeBuilder<DocumentPage> builder)
    {
        builder.ToTable("DocumentPages");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .UseIdentityColumn();

        builder.Property(p => p.DocumentId)
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        builder.Property(p => p.PageNumber)
            .IsRequired();

        builder.Property(p => p.ExtractedText)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.HasIndex(p => new { p.DocumentId, p.PageNumber })
            .IsUnique();
    }
}
