using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodTraceability.Modules.Quality.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddLotBlock : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "lot_block",
            schema: "quality",
            columns: table => new
            {
                lot_block_id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                blocked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                blocked_by = table.Column<Guid>(type: "uuid", nullable: false),
                released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                released_by = table.Column<Guid>(type: "uuid", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_lot_block", x => x.lot_block_id);
                table.CheckConstraint("ck_lot_block_release_pair",
                    "(released_at IS NULL) = (released_by IS NULL)");
            });

        migrationBuilder.CreateIndex(
            name: "ux_lot_block_lot_id_open",
            schema: "quality",
            table: "lot_block",
            column: "lot_id",
            unique: true,
            filter: "released_at IS NULL");

        // D-11/D-46: migration-only FK to ak_lot_lot_id_organization_id.
        migrationBuilder.AddForeignKey(
            name: "fk_lot_block_trace_lot",
            schema: "quality",
            table: "lot_block",
            columns: ["lot_id", "organization_id"],
            principalSchema: "trace",
            principalTable: "lot",
            principalColumns: ["lot_id", "organization_id"],
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "fk_lot_block_blocked_by_identity_user",
            schema: "quality",
            table: "lot_block",
            column: "blocked_by",
            principalSchema: "identity",
            principalTable: "user",
            principalColumn: "user_id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "fk_lot_block_released_by_identity_user",
            schema: "quality",
            table: "lot_block",
            column: "released_by",
            principalSchema: "identity",
            principalTable: "user",
            principalColumn: "user_id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "lot_block", schema: "quality");
    }
}
