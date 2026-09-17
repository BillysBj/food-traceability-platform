using FoodTraceability.Modules.Documents.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FoodTraceability.Modules.Documents.Infrastructure.Configurations;

internal sealed class DocumentTypeConfiguration : IEntityTypeConfiguration<DocumentType>
{
    private static readonly ValueConverter<DocumentTypeCode, string> CodeConverter = new(
        code => code.Value,
        value => DocumentTypeCode.Create(value));

    public void Configure(EntityTypeBuilder<DocumentType> builder)
    {
        builder.ToTable("document_type", DocumentsDbContext.Schema);
        builder.HasKey(type => type.Id);
        builder.Property(type => type.Id).HasColumnName("document_type_id").ValueGeneratedNever();
        builder.Property(type => type.Code)
            .HasConversion(CodeConverter)
            .HasMaxLength(DocumentTypeCode.MaximumLength)
            .IsRequired();
        builder.HasIndex(type => type.Code).IsUnique();

        builder.HasData(
            DocumentType.Create(StandardDocumentTypeIds.LabReport, DocumentTypeCode.Create("LAB_REPORT")),
            DocumentType.Create(StandardDocumentTypeIds.Certificate, DocumentTypeCode.Create("CERTIFICATE")),
            DocumentType.Create(StandardDocumentTypeIds.DeliveryNote, DocumentTypeCode.Create("DELIVERY_NOTE")));
    }
}
