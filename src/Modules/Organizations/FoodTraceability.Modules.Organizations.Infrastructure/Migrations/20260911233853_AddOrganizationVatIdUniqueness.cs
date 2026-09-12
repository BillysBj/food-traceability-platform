using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodTraceability.Modules.Organizations.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddOrganizationVatIdUniqueness : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "ux_organization_vat_id",
            schema: "org",
            table: "organization",
            column: "vat_id",
            unique: true,
            filter: "vat_id IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_organization_vat_id",
            schema: "org",
            table: "organization");
    }
}
