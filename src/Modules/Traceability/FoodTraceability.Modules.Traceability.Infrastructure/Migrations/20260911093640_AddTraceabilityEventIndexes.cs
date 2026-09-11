using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodTraceability.Modules.Traceability.Infrastructure.Migrations;

public partial class AddTraceabilityEventIndexes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "ix_traceability_event_occurred_at",
            schema: "trace",
            table: "traceability_event",
            column: "occurred_at");

        migrationBuilder.CreateIndex(
            name: "ix_traceability_event_organization_id",
            schema: "trace",
            table: "traceability_event",
            column: "organization_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_traceability_event_occurred_at",
            schema: "trace",
            table: "traceability_event");

        migrationBuilder.DropIndex(
            name: "ix_traceability_event_organization_id",
            schema: "trace",
            table: "traceability_event");
    }
}
