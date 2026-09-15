using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodTraceability.Modules.Quality.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddSpecification : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "specification",
            schema: "quality",
            columns: table => new
            {
                specification_id = table.Column<Guid>(type: "uuid", nullable: false),
                article_id = table.Column<Guid>(type: "uuid", nullable: false),
                version = table.Column<int>(type: "integer", nullable: false),
                valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                valid_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_specification", x => x.specification_id);
                table.CheckConstraint("ck_specification_version", "version > 0");
                table.CheckConstraint("ck_specification_validity", "valid_to IS NULL OR valid_to >= valid_from");
            });

        // D-11/D-52: configuration belongs to an article. Reference its primary key;
        // there is no organization_id here and no cross-module entity in the EF model.
        migrationBuilder.AddForeignKey(
            name: "fk_specification_catalog_article",
            schema: "quality",
            table: "specification",
            column: "article_id",
            principalSchema: "catalog",
            principalTable: "article",
            principalColumn: "article_id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.CreateTable(
            name: "specification_parameter",
            schema: "quality",
            columns: table => new
            {
                spec_parameter_id = table.Column<Guid>(type: "uuid", nullable: false),
                specification_id = table.Column<Guid>(type: "uuid", nullable: false),
                parameter_id = table.Column<Guid>(type: "uuid", nullable: false),
                min = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                max = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                target = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                required = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_specification_parameter", x => x.spec_parameter_id);
                table.CheckConstraint("ck_specification_parameter_bounds", "min IS NULL OR max IS NULL OR min <= max");
                table.ForeignKey(
                    name: "fk_specification_parameter_parameter",
                    column: x => x.parameter_id,
                    principalSchema: "quality",
                    principalTable: "parameter",
                    principalColumn: "parameter_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_specification_parameter_specification",
                    column: x => x.specification_id,
                    principalSchema: "quality",
                    principalTable: "specification",
                    principalColumn: "specification_id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ux_specification_article_id_version",
            schema: "quality",
            table: "specification",
            columns: ["article_id", "version"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_specification_parameter_parameter_id",
            schema: "quality",
            table: "specification_parameter",
            column: "parameter_id");

        migrationBuilder.CreateIndex(
            name: "ux_specification_parameter_specification_id_parameter_id",
            schema: "quality",
            table: "specification_parameter",
            columns: ["specification_id", "parameter_id"],
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "specification_parameter", schema: "quality");
        migrationBuilder.DropTable(name: "specification", schema: "quality");
    }
}
