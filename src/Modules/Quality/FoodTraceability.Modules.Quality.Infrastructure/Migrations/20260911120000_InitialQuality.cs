using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodTraceability.Modules.Quality.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialQuality : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "quality");

        migrationBuilder.CreateTable(
            name: "parameter",
            schema: "quality",
            columns: table => new
            {
                parameter_id = table.Column<Guid>(type: "uuid", nullable: false),
                code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                standard_method = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_parameter", x => x.parameter_id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_parameter_code",
            schema: "quality",
            table: "parameter",
            column: "code",
            unique: true);

        // The foreign key is declared here rather than in the EF model, because the
        // Quality module must not reference the Catalog module (D-11).
        migrationBuilder.AddForeignKey(
            name: "fk_parameter_catalog_unit",
            schema: "quality",
            table: "parameter",
            column: "unit_id",
            principalSchema: "catalog",
            principalTable: "unit",
            principalColumn: "unit_id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "parameter", schema: "quality");
    }
}
