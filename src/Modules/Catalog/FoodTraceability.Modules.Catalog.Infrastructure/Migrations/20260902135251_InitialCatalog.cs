using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodTraceability.Modules.Catalog.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialCatalog : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "catalog");

        migrationBuilder.CreateTable(
            name: "product",
            schema: "catalog",
            columns: table => new
            {
                product_id = table.Column<Guid>(type: "uuid", nullable: false),
                product_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_product", x => x.product_id);
            });

        migrationBuilder.Sql(
            """
            CREATE UNIQUE INDEX ux_product_product_code_upper
                ON catalog.product (upper(product_code));
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "product",
            schema: "catalog");
    }
}
