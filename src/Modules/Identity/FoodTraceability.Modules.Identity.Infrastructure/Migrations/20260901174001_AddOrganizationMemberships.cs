using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FoodTraceability.Modules.Identity.Infrastructure.Migrations;
/// <inheritdoc />
public partial class AddOrganizationMemberships : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
                name: "assignment_scope",
                schema: "identity",
                table: "role",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            // Redundant with the primary key by design: composite scope-enforcing FKs need
            // a matching PK/UNIQUE target on (role_id, assignment_scope).
            migrationBuilder.AddUniqueConstraint(
                name: "ak_roles_id_assignment_scope",
            schema: "identity",
            table: "role",
            columns: new[] { "role_id", "assignment_scope" });

        migrationBuilder.CreateTable(
            name: "organization_membership",
            schema: "identity",
            columns: table => new
            {
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_organization_membership", x => new { x.user_id, x.organization_id });
                table.ForeignKey(
                    name: "fk_organization_membership_user_user_id",
                    column: x => x.user_id,
                    principalSchema: "identity",
                    principalTable: "user",
                    principalColumn: "user_id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "platform_role_assignment",
            schema: "identity",
            columns: table => new
            {
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                role_id = table.Column<Guid>(type: "uuid", nullable: false),
                assignment_scope = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_platform_role_assignment", x => new { x.user_id, x.role_id });
                table.CheckConstraint("ck_platform_role_assignment_assignment_scope", "assignment_scope = 'PLATFORM'");
                table.ForeignKey(
                    name: "fk_platform_role_assignment_roles_role_id_assignment_scope",
                    columns: x => new { x.role_id, x.assignment_scope },
                    principalSchema: "identity",
                    principalTable: "role",
                    principalColumns: new[] { "role_id", "assignment_scope" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_platform_role_assignment_user_user_id",
                    column: x => x.user_id,
                    principalSchema: "identity",
                    principalTable: "user",
                    principalColumn: "user_id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "organization_role_assignment",
            schema: "identity",
            columns: table => new
            {
                organization_role_assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                role_id = table.Column<Guid>(type: "uuid", nullable: false),
                location_id = table.Column<Guid>(type: "uuid", nullable: true),
                assignment_scope = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_organization_role_assignment", x => x.organization_role_assignment_id);
                table.CheckConstraint("ck_organization_role_assignment_assignment_scope", "assignment_scope = 'ORGANIZATION'");
                table.ForeignKey(
                    name: "fk_organization_role_assignment_organization_membership_user_i",
                    columns: x => new { x.user_id, x.organization_id },
                    principalSchema: "identity",
                    principalTable: "organization_membership",
                    principalColumns: new[] { "user_id", "organization_id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_organization_role_assignment_roles_role_id_assignment_scope",
                    columns: x => new { x.role_id, x.assignment_scope },
                    principalSchema: "identity",
                    principalTable: "role",
                    principalColumns: new[] { "role_id", "assignment_scope" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.UpdateData(
                schema: "identity",
                table: "role",
                keyColumn: "role_id",
                keyValue: new Guid("0002868f-4330-5c7c-aac4-77420d2aff52"),
                column: "assignment_scope",
                value: "ORGANIZATION");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role",
                keyColumn: "role_id",
                keyValue: new Guid("00ec29aa-1bc7-540e-b04d-02c3497f50b3"),
                column: "assignment_scope",
                value: "ORGANIZATION");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role",
                keyColumn: "role_id",
                keyValue: new Guid("222d9d3e-a711-5607-820a-59c9f497bbaf"),
                column: "assignment_scope",
                value: "ORGANIZATION");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role",
                keyColumn: "role_id",
                keyValue: new Guid("2ef0f055-298c-512e-a35e-10d180133f51"),
                column: "assignment_scope",
                value: "ORGANIZATION");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role",
                keyColumn: "role_id",
                keyValue: new Guid("34644a1b-9bbb-5005-98a1-b3584dd8bf69"),
                column: "assignment_scope",
                value: "ORGANIZATION");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role",
                keyColumn: "role_id",
                keyValue: new Guid("351aaedc-26d7-5406-9109-f4f8139ec1a8"),
                column: "assignment_scope",
                value: "ORGANIZATION");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role",
                keyColumn: "role_id",
                keyValue: new Guid("38623612-6286-55ce-9af2-ea523f760be3"),
                column: "assignment_scope",
                value: "ORGANIZATION");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role",
                keyColumn: "role_id",
                keyValue: new Guid("a187d055-fa04-56ef-b488-2b6ad6216007"),
                column: "assignment_scope",
                value: "ORGANIZATION");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role",
                keyColumn: "role_id",
                keyValue: new Guid("d8c0f985-5ce5-59b9-bf3b-71ae6bc5616a"),
                column: "assignment_scope",
                value: "ORGANIZATION");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role",
                keyColumn: "role_id",
                keyValue: new Guid("ec72b8b5-2610-5efd-aa7f-6aa59889da7d"),
                column: "assignment_scope",
                value: "PLATFORM");

            migrationBuilder.AlterColumn<string>(
                name: "assignment_scope",
                schema: "identity",
                table: "role",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(12)",
                oldMaxLength: 12,
                oldNullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_role_assignment_scope",
            schema: "identity",
            table: "role",
            sql: "assignment_scope IN ('PLATFORM', 'ORGANIZATION')");

        migrationBuilder.CreateIndex(
            name: "ix_organization_role_assignment_role_id_assignment_scope",
            schema: "identity",
            table: "organization_role_assignment",
            columns: new[] { "role_id", "assignment_scope" });

        migrationBuilder.CreateIndex(
            name: "ix_organization_role_assignment_user_id_organization_id_role_i",
            schema: "identity",
            table: "organization_role_assignment",
            columns: new[] { "user_id", "organization_id", "role_id", "location_id" },
            unique: true)
            .Annotation("Npgsql:NullsDistinct", false);

        migrationBuilder.CreateIndex(
                name: "ix_platform_role_assignment_role_id_assignment_scope",
                schema: "identity",
                table: "platform_role_assignment",
                columns: new[] { "role_id", "assignment_scope" });

            migrationBuilder.AddForeignKey(
                name: "fk_org_membership_org_organization",
                schema: "identity",
                table: "organization_membership",
                column: "organization_id",
                principalSchema: "org",
                principalTable: "organization",
                principalColumn: "organization_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_org_role_assignment_org_location",
                schema: "identity",
                table: "organization_role_assignment",
                columns: new[] { "location_id", "organization_id" },
                principalSchema: "org",
                principalTable: "location",
                principalColumns: new[] { "location_id", "organization_id" },
                onDelete: ReferentialAction.Restrict);
        }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "organization_role_assignment",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "platform_role_assignment",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "organization_membership",
            schema: "identity");

        migrationBuilder.DropUniqueConstraint(
            name: "ak_roles_id_assignment_scope",
            schema: "identity",
            table: "role");

        migrationBuilder.DropCheckConstraint(
            name: "ck_role_assignment_scope",
            schema: "identity",
            table: "role");

        migrationBuilder.DropColumn(
            name: "assignment_scope",
            schema: "identity",
            table: "role");
    }
}
