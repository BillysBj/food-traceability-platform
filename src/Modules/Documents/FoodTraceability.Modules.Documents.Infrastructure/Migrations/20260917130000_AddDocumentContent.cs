using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodTraceability.Modules.Documents.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddDocumentContent : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "document_content",
            schema: "docs",
            columns: table => new
            {
                storage_key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                content = table.Column<byte[]>(type: "bytea", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_document_content", x => x.storage_key);
                table.CheckConstraint("ck_document_content_not_empty", "octet_length(content) > 0");
            });

        migrationBuilder.AddForeignKey(
            name: "fk_document_document_content_storage_key",
            schema: "docs",
            table: "document",
            column: "storage_key",
            principalSchema: "docs",
            principalTable: "document_content",
            principalColumn: "storage_key",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_document_document_content_storage_key",
            schema: "docs",
            table: "document");

        migrationBuilder.DropTable(name: "document_content", schema: "docs");
    }
}
