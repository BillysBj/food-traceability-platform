using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodTraceability.Modules.Quality.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddLabResult : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "lab_result",
            schema: "quality",
            columns: table => new
            {
                lab_result_id = table.Column<Guid>(type: "uuid", nullable: false),
                sample_id = table.Column<Guid>(type: "uuid", nullable: false),
                parameter_id = table.Column<Guid>(type: "uuid", nullable: false),
                value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                assessment = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                method = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                measured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_lab_result", x => x.lab_result_id);
                table.CheckConstraint("ck_lab_result_assessment", "assessment IN ('PASS', 'FAIL')");
                table.ForeignKey(
                    name: "fk_lab_result_parameter",
                    column: x => x.parameter_id,
                    principalSchema: "quality",
                    principalTable: "parameter",
                    principalColumn: "parameter_id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_lab_result_sample",
                    column: x => x.sample_id,
                    principalSchema: "quality",
                    principalTable: "sample",
                    principalColumn: "sample_id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_lab_result_parameter_id",
            schema: "quality",
            table: "lab_result",
            column: "parameter_id");

        migrationBuilder.CreateIndex(
            name: "ux_lab_result_sample_id_parameter_id",
            schema: "quality",
            table: "lab_result",
            columns: ["sample_id", "parameter_id"],
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "lab_result", schema: "quality");
    }
}
