using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodTraceability.Modules.Traceability.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddTraceabilityEvent : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddUniqueConstraint(
            name: "ak_lot_lot_id_organization_id_unit_id",
            schema: "trace",
            table: "lot",
            columns: new[] { "lot_id", "organization_id", "unit_id" });

        migrationBuilder.CreateTable(
            name: "traceability_event",
            schema: "trace",
            columns: table => new
            {
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                event_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                location_id = table.Column<Guid>(type: "uuid", nullable: false),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                external_reference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                created_by = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_traceability_event", x => x.event_id);
                table.UniqueConstraint("ak_traceability_event_event_id_organization_id", x => new { x.event_id, x.organization_id });
                table.ForeignKey(
                    name: "fk_traceability_event_trace_event_type",
                    column: x => x.event_type_id,
                    principalSchema: "trace",
                    principalTable: "event_type",
                    principalColumn: "event_type_id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "event_input",
            schema: "trace",
            columns: table => new
            {
                event_input_id = table.Column<Guid>(type: "uuid", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                unit_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_event_input", x => x.event_input_id);
                table.CheckConstraint("ck_event_input_quantity_positive", "quantity > 0");
                table.ForeignKey(
                    name: "fk_event_input_trace_lot",
                    columns: x => new { x.lot_id, x.organization_id, x.unit_id },
                    principalSchema: "trace",
                    principalTable: "lot",
                    principalColumns: new[] { "lot_id", "organization_id", "unit_id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_event_input_trace_traceability_event",
                    columns: x => new { x.event_id, x.organization_id },
                    principalSchema: "trace",
                    principalTable: "traceability_event",
                    principalColumns: new[] { "event_id", "organization_id" },
                    onDelete: ReferentialAction.Cascade);
            });

        // These foreign keys are declared here rather than in the EF model because the
        // Traceability module must not reference Organizations or Identity (D-11). The
        // location FK targets ak_location_location_id_organization_id, so an existing
        // location from another organization is rejected just like an unknown location.
        migrationBuilder.AddForeignKey(
            name: "fk_traceability_event_org_organization",
            schema: "trace",
            table: "traceability_event",
            column: "organization_id",
            principalSchema: "org",
            principalTable: "organization",
            principalColumn: "organization_id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "fk_traceability_event_org_location",
            schema: "trace",
            table: "traceability_event",
            columns: ["location_id", "organization_id"],
            principalSchema: "org",
            principalTable: "location",
            principalColumns: ["location_id", "organization_id"],
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "fk_traceability_event_identity_user",
            schema: "trace",
            table: "traceability_event",
            column: "created_by",
            principalSchema: "identity",
            principalTable: "user",
            principalColumn: "user_id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.CreateTable(
            name: "event_output",
            schema: "trace",
            columns: table => new
            {
                event_output_id = table.Column<Guid>(type: "uuid", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                unit_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_event_output", x => x.event_output_id);
                table.CheckConstraint("ck_event_output_quantity_positive", "quantity > 0");
                table.ForeignKey(
                    name: "fk_event_output_trace_lot",
                    columns: x => new { x.lot_id, x.organization_id, x.unit_id },
                    principalSchema: "trace",
                    principalTable: "lot",
                    principalColumns: new[] { "lot_id", "organization_id", "unit_id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_event_output_trace_traceability_event",
                    columns: x => new { x.event_id, x.organization_id },
                    principalSchema: "trace",
                    principalTable: "traceability_event",
                    principalColumns: new[] { "event_id", "organization_id" },
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_event_input_event_id_organization_id",
            schema: "trace",
            table: "event_input",
            columns: new[] { "event_id", "organization_id" });

        migrationBuilder.CreateIndex(
            name: "ix_event_input_lot_id",
            schema: "trace",
            table: "event_input",
            column: "lot_id");

        migrationBuilder.CreateIndex(
            name: "ix_event_input_lot_id_organization_id_unit_id",
            schema: "trace",
            table: "event_input",
            columns: new[] { "lot_id", "organization_id", "unit_id" });

        migrationBuilder.CreateIndex(
            name: "ux_event_input_event_id_lot_id",
            schema: "trace",
            table: "event_input",
            columns: new[] { "event_id", "lot_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_event_output_event_id_organization_id",
            schema: "trace",
            table: "event_output",
            columns: new[] { "event_id", "organization_id" });

        migrationBuilder.CreateIndex(
            name: "ix_event_output_lot_id",
            schema: "trace",
            table: "event_output",
            column: "lot_id");

        migrationBuilder.CreateIndex(
            name: "ix_event_output_lot_id_organization_id_unit_id",
            schema: "trace",
            table: "event_output",
            columns: new[] { "lot_id", "organization_id", "unit_id" });

        migrationBuilder.CreateIndex(
            name: "ux_event_output_event_id_lot_id",
            schema: "trace",
            table: "event_output",
            columns: new[] { "event_id", "lot_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_traceability_event_event_type_id",
            schema: "trace",
            table: "traceability_event",
            column: "event_type_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "event_input",
            schema: "trace");

        migrationBuilder.DropTable(
            name: "event_output",
            schema: "trace");

        migrationBuilder.DropTable(
            name: "traceability_event",
            schema: "trace");

        migrationBuilder.DropUniqueConstraint(
            name: "ak_lot_lot_id_organization_id_unit_id",
            schema: "trace",
            table: "lot");
    }
}
