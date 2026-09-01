using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FoodTraceability.Modules.Identity.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddRoles : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "role",
            schema: "identity",
            columns: table => new
            {
                role_id = table.Column<Guid>(type: "uuid", nullable: false),
                code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_role", x => x.role_id);
            });

        migrationBuilder.InsertData(
            schema: "identity",
            table: "role",
            columns: new[] { "role_id", "code", "description", "name" },
            values: new object[,]
            {
                    { new Guid("0002868f-4330-5c7c-aac4-77420d2aff52"), "RETAILER", null, "Retailer" },
                    { new Guid("00ec29aa-1bc7-540e-b04d-02c3497f50b3"), "ORGANIZATION_ADMIN", null, "OrganizationAdmin" },
                    { new Guid("222d9d3e-a711-5607-820a-59c9f497bbaf"), "LOGISTICS", null, "Logistics" },
                    { new Guid("2ef0f055-298c-512e-a35e-10d180133f51"), "LABORATORY", null, "Laboratory" },
                    { new Guid("34644a1b-9bbb-5005-98a1-b3584dd8bf69"), "BOTTLER", null, "Bottler" },
                    { new Guid("351aaedc-26d7-5406-9109-f4f8139ec1a8"), "PROCESSOR", null, "Processor" },
                    { new Guid("38623612-6286-55ce-9af2-ea523f760be3"), "QUALITY_MANAGER", null, "QualityManager" },
                    { new Guid("a187d055-fa04-56ef-b488-2b6ad6216007"), "AUDITOR", null, "Auditor" },
                    { new Guid("d8c0f985-5ce5-59b9-bf3b-71ae6bc5616a"), "PRODUCER", null, "Producer" },
                    { new Guid("ec72b8b5-2610-5efd-aa7f-6aa59889da7d"), "PLATFORM_ADMIN", null, "PlatformAdmin" }
            });

        migrationBuilder.CreateIndex(
            name: "ix_role_code",
            schema: "identity",
            table: "role",
            column: "code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_role_name",
            schema: "identity",
            table: "role",
            column: "name",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "role",
            schema: "identity");
    }
}
