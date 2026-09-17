using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodTraceability.Modules.Documents.Infrastructure.Migrations;

[DbContext(typeof(DocumentsDbContext))]
[Migration("20260917130000_AddDocumentContent")]
partial class AddDocumentContent
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.11")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity("FoodTraceability.Modules.Documents.Domain.Document", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("uuid")
                .HasColumnName("document_id");

            b.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");

            b.Property<DateOnly>("DocumentDate")
                .HasColumnType("date")
                .HasColumnName("document_date");

            b.Property<Guid>("DocumentTypeId")
                .HasColumnType("uuid")
                .HasColumnName("document_type_id");

            b.Property<string>("FileName")
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnType("character varying(255)")
                .HasColumnName("file_name");

            b.Property<string>("MimeType")
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnType("character varying(255)")
                .HasColumnName("mime_type");

            b.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("character varying(200)")
                .HasColumnName("name");

            b.Property<Guid>("OrganizationId")
                .HasColumnType("uuid")
                .HasColumnName("organization_id");

            b.Property<string>("Sha256")
                .IsRequired()
                .HasMaxLength(64)
                .HasColumnType("character(64)")
                .HasColumnName("sha256");

            b.Property<string>("StorageKey")
                .IsRequired()
                .HasMaxLength(1024)
                .HasColumnType("character varying(1024)")
                .HasColumnName("storage_key");

            b.HasKey("Id")
                .HasName("pk_document");

            b.HasIndex("DocumentTypeId")
                .HasDatabaseName("ix_document_document_type_id");

            b.HasIndex("StorageKey")
                .IsUnique()
                .HasDatabaseName("ix_document_storage_key");

            b.ToTable("document", "docs", t =>
            {
                t.HasCheckConstraint("ck_document_sha256", "sha256 ~ '^[0-9a-f]{64}$'");
            });
        });

        modelBuilder.Entity("FoodTraceability.Modules.Documents.Domain.DocumentContent", b =>
        {
            b.Property<string>("StorageKey")
                .HasMaxLength(1024)
                .HasColumnType("character varying(1024)")
                .HasColumnName("storage_key");

            b.Property<byte[]>("Content")
                .IsRequired()
                .HasColumnType("bytea")
                .HasColumnName("content");

            b.HasKey("StorageKey")
                .HasName("pk_document_content");

            b.ToTable("document_content", "docs", t =>
            {
                t.HasCheckConstraint("ck_document_content_not_empty", "octet_length(content) > 0");
            });
        });

        modelBuilder.Entity("FoodTraceability.Modules.Documents.Domain.DocumentType", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("uuid")
                .HasColumnName("document_type_id");

            b.Property<string>("Code")
                .IsRequired()
                .HasMaxLength(64)
                .HasColumnType("character varying(64)")
                .HasColumnName("code");

            b.HasKey("Id")
                .HasName("pk_document_type");

            b.HasIndex("Code")
                .IsUnique()
                .HasDatabaseName("ix_document_type_code");

            b.ToTable("document_type", "docs");

            b.HasData(
                new
                {
                    Id = new Guid("5b9827b8-61e4-5046-8787-ff0d1418b4a7"),
                    Code = "LAB_REPORT"
                },
                new
                {
                    Id = new Guid("b4f4b593-4b19-5f13-8d15-b1d51e126862"),
                    Code = "CERTIFICATE"
                },
                new
                {
                    Id = new Guid("ae9fd5c5-e18b-5bf4-ac1a-b7ae088e14aa"),
                    Code = "DELIVERY_NOTE"
                });
        });

        modelBuilder.Entity("FoodTraceability.Modules.Documents.Domain.Document", b =>
        {
            b.HasOne("FoodTraceability.Modules.Documents.Domain.DocumentContent", null)
                .WithMany()
                .HasForeignKey("StorageKey")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired()
                .HasConstraintName("fk_document_document_content_storage_key");

            b.HasOne("FoodTraceability.Modules.Documents.Domain.DocumentType", null)
                .WithMany()
                .HasForeignKey("DocumentTypeId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired()
                .HasConstraintName("fk_document_document_type_document_type_id");
        });
    }
}
