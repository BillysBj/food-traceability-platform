using FoodTraceability.Modules.Documents.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodTraceability.Modules.Documents.Infrastructure.Configurations;

internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("document", DocumentsDbContext.Schema,
            table => table.HasCheckConstraint("ck_document_sha256", "sha256 ~ '^[0-9a-f]{64}$'"));
        builder.HasKey(document => document.Id);
        builder.Property(document => document.Id).HasColumnName("document_id").ValueGeneratedNever();
        builder.Property(document => document.DocumentTypeId).IsRequired();
        builder.HasOne<DocumentType>().WithMany()
            .HasForeignKey(document => document.DocumentTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // The cross-schema FK to org.organization is declared only in the migration (D-11).
        builder.Property(document => document.OrganizationId).IsRequired();
        builder.Property(document => document.Name).HasMaxLength(Document.MaximumNameLength).IsRequired();
        builder.Property(document => document.FileName).HasMaxLength(Document.MaximumFileNameLength).IsRequired();
        builder.Property(document => document.StorageKey).HasMaxLength(Document.MaximumStorageKeyLength).IsRequired();
        builder.HasIndex(document => document.StorageKey).IsUnique();
        builder.HasOne<DocumentContent>().WithMany()
            .HasForeignKey(document => document.StorageKey)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(document => document.MimeType).HasMaxLength(Document.MaximumMimeTypeLength).IsRequired();
        builder.Property(document => document.DocumentDate).HasColumnType("date").IsRequired();
        builder.Property(document => document.Sha256)
            .HasColumnType("character(64)")
            .HasMaxLength(Document.Sha256Length)
            .IsRequired();
        builder.Property(document => document.CreatedAt).IsRequired();
    }
}
