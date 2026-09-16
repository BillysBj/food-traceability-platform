using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodTraceability.Modules.Traceability.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddLotQualityStatus : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // D-53: initialize every existing lot. New lots get Pending from Lot.Create.
        migrationBuilder.AddColumn<string>(
            name: "quality_status",
            schema: "trace",
            table: "lot",
            type: "character varying(8)",
            maxLength: 8,
            nullable: false,
            defaultValue: "PENDING");

        migrationBuilder.Sql("ALTER TABLE trace.lot ALTER COLUMN quality_status DROP DEFAULT;");

        migrationBuilder.AddCheckConstraint(
            name: "ck_lot_quality_status",
            schema: "trace",
            table: "lot",
            sql: "quality_status IN ('PENDING', 'BLOCKED', 'RELEASED')");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_lot_quality_status", schema: "trace", table: "lot");
        migrationBuilder.DropColumn(name: "quality_status", schema: "trace", table: "lot");
    }
}
