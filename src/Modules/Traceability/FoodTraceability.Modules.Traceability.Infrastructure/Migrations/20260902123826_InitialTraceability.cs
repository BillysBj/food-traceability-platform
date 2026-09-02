using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodTraceability.Modules.Traceability.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialTraceability : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "trace");

        migrationBuilder.CreateTable(
            name: "lot",
            schema: "trace",
            columns: table => new
            {
                lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                lot_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_lot", x => x.lot_id);
            });

        migrationBuilder.AddForeignKey(
            name: "fk_lot_org_organization",
            schema: "trace",
            table: "lot",
            column: "organization_id",
            principalSchema: "org",
            principalTable: "organization",
            principalColumn: "organization_id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.Sql(
            """
            CREATE UNIQUE INDEX ux_lot_organization_id_lot_number_upper
                ON trace.lot (organization_id, upper(lot_number));
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "lot",
            schema: "trace");
    }
}
