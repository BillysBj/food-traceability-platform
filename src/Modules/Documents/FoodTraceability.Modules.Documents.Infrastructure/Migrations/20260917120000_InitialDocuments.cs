using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodTraceability.Modules.Documents.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialDocuments : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "docs");

        migrationBuilder.CreateTable(
            name: "document_type",
            schema: "docs",
            columns: table => new
            {
                document_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_document_type", x => x.document_type_id);
            });

        migrationBuilder.CreateTable(
            name: "document",
            schema: "docs",
            columns: table => new
            {
                document_id = table.Column<Guid>(type: "uuid", nullable: false),
                document_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                storage_key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                mime_type = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                document_date = table.Column<DateOnly>(type: "date", nullable: false),
                sha256 = table.Column<string>(type: "character(64)", maxLength: 64, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_document", x => x.document_id);
                table.CheckConstraint("ck_document_sha256", "sha256 ~ '^[0-9a-f]{64}$'");
                table.ForeignKey(
                    name: "fk_document_document_type_document_type_id",
                    column: x => x.document_type_id,
                    principalSchema: "docs",
                    principalTable: "document_type",
                    principalColumn: "document_type_id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.InsertData(
            schema: "docs",
            table: "document_type",
            columns: new[] { "document_type_id", "code" },
            values: new object[,]
            {
                { new Guid("5b9827b8-61e4-5046-8787-ff0d1418b4a7"), "LAB_REPORT" },
                { new Guid("b4f4b593-4b19-5f13-8d15-b1d51e126862"), "CERTIFICATE" },
                { new Guid("ae9fd5c5-e18b-5bf4-ac1a-b7ae088e14aa"), "DELIVERY_NOTE" }
            });

        migrationBuilder.CreateIndex(
            name: "ix_document_type_code",
            schema: "docs",
            table: "document_type",
            column: "code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_document_document_type_id",
            schema: "docs",
            table: "document",
            column: "document_type_id");

        migrationBuilder.CreateIndex(
            name: "ix_document_storage_key",
            schema: "docs",
            table: "document",
            column: "storage_key",
            unique: true);

        // The foreign key is declared here rather than in the EF model, because the
        // Documents module must not reference the Organizations module (D-11).
        migrationBuilder.AddForeignKey(
            name: "fk_document_org_organization",
            schema: "docs",
            table: "document",
            column: "organization_id",
            principalSchema: "org",
            principalTable: "organization",
            principalColumn: "organization_id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "document", schema: "docs");
        migrationBuilder.DropTable(name: "document_type", schema: "docs");
    }
}
