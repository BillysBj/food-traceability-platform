using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodTraceability.Modules.Traceability.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddLotOrganizationAlternateKey : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // D-46: quality.sample needs a tenant-safe lot reference without a unit id.
        migrationBuilder.AddUniqueConstraint(
            name: "ak_lot_lot_id_organization_id",
            schema: "trace",
            table: "lot",
            columns: new[] { "lot_id", "organization_id" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropUniqueConstraint(
            name: "ak_lot_lot_id_organization_id",
            schema: "trace",
            table: "lot");
    }
}
