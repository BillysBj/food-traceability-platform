using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodTraceability.Modules.Organizations.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialOrganizations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "org");

        migrationBuilder.CreateTable(
            name: "organization",
            schema: "org",
            columns: table => new
            {
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                vat_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                tax_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_organization", x => x.organization_id);
            });

        migrationBuilder.CreateTable(
            name: "location",
            schema: "org",
            columns: table => new
            {
                location_id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                region = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                country_code = table.Column<string>(type: "character(2)", maxLength: 2, nullable: true),
                latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_location", x => x.location_id);
                table.UniqueConstraint("ak_location_location_id_organization_id", x => new { x.location_id, x.organization_id });
                table.CheckConstraint("ck_location_coordinates_complete", "(latitude IS NULL AND longitude IS NULL) OR (latitude IS NOT NULL AND longitude IS NOT NULL)");
                table.CheckConstraint("ck_location_latitude_range", "latitude IS NULL OR latitude BETWEEN -90 AND 90");
                table.CheckConstraint("ck_location_longitude_range", "longitude IS NULL OR longitude BETWEEN -180 AND 180");
                table.ForeignKey(
                    name: "fk_location_organization_organization_id",
                    column: x => x.organization_id,
                    principalSchema: "org",
                    principalTable: "organization",
                    principalColumn: "organization_id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_location_organization_id",
            schema: "org",
            table: "location",
            column: "organization_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "location",
            schema: "org");

        migrationBuilder.DropTable(
            name: "organization",
            schema: "org");
    }
}
