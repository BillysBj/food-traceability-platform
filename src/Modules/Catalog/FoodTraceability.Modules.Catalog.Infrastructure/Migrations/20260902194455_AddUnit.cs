using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FoodTraceability.Modules.Catalog.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddUnit : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "unit",
            schema: "catalog",
            columns: table => new
            {
                unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                symbol = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                dimension = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_unit", x => x.unit_id);
                table.CheckConstraint("ck_unit_dimension", "dimension IN ('MASS', 'VOLUME', 'COUNT')");
            });

        migrationBuilder.InsertData(
            schema: "catalog",
            table: "unit",
            columns: new[] { "unit_id", "code", "created_at", "dimension", "symbol" },
            values: new object[,]
            {
                    { new Guid("4ba563a7-f314-57d8-b3d7-ee5c12ff1085"), "KG", new DateTimeOffset(new DateTime(2026, 9, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "MASS", "kg" },
                    { new Guid("5e726b86-c672-5ed0-9601-904328038341"), "G", new DateTimeOffset(new DateTime(2026, 9, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "MASS", "g" },
                    { new Guid("8d8ed466-8384-5e44-8430-eee76f15a180"), "L", new DateTimeOffset(new DateTime(2026, 9, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "VOLUME", "l" },
                    { new Guid("d227d884-ef6c-5667-9587-1d9fdee6836e"), "PCS", new DateTimeOffset(new DateTime(2026, 9, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "COUNT", "pcs" },
                    { new Guid("dd541026-8821-53a3-97de-f0a974327970"), "ML", new DateTimeOffset(new DateTime(2026, 9, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "VOLUME", "ml" }
            });

        migrationBuilder.CreateIndex(
            name: "ix_unit_code",
            schema: "catalog",
            table: "unit",
            column: "code",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "unit",
            schema: "catalog");
    }
}
