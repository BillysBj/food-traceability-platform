using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodTraceability.Modules.Quality.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddSample : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "sample",
            schema: "quality",
            columns: table => new
            {
                sample_id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                location_id = table.Column<Guid>(type: "uuid", nullable: false),
                traceability_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                sample_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                taken_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                status = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_sample", x => x.sample_id);
                table.CheckConstraint("ck_sample_status", "status IN ('PENDING', 'PASS', 'FAIL')");
            });

        migrationBuilder.Sql(
            "CREATE UNIQUE INDEX ux_sample_organization_id_sample_number_upper ON quality.sample (organization_id, UPPER(sample_number));");

        // D-11/D-46: cross-module references exist only in the migration. Each
        // composite FK enforces organization equality as well as existence.
        migrationBuilder.AddForeignKey(
            name: "fk_sample_trace_traceability_event",
            schema: "quality",
            table: "sample",
            columns: ["traceability_event_id", "organization_id"],
            principalSchema: "trace",
            principalTable: "traceability_event",
            principalColumns: ["event_id", "organization_id"],
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "fk_sample_trace_lot",
            schema: "quality",
            table: "sample",
            columns: ["lot_id", "organization_id"],
            principalSchema: "trace",
            principalTable: "lot",
            principalColumns: ["lot_id", "organization_id"],
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "fk_sample_org_location",
            schema: "quality",
            table: "sample",
            columns: ["location_id", "organization_id"],
            principalSchema: "org",
            principalTable: "location",
            principalColumns: ["location_id", "organization_id"],
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "sample", schema: "quality");
    }
}
