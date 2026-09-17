using FoodTraceability.Modules.Documents.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodTraceability.Modules.Documents.Infrastructure.Configurations;

internal sealed class DocumentContentConfiguration : IEntityTypeConfiguration<DocumentContent>
{
    public void Configure(EntityTypeBuilder<DocumentContent> builder)
    {
        builder.ToTable("document_content", DocumentsDbContext.Schema,
            table => table.HasCheckConstraint("ck_document_content_not_empty", "octet_length(content) > 0"));
        builder.HasKey(content => content.StorageKey);
        builder.Property(content => content.StorageKey)
            .HasMaxLength(Document.MaximumStorageKeyLength).IsRequired().ValueGeneratedNever();
        builder.Property(content => content.Content).HasColumnType("bytea").IsRequired();
    }
}
