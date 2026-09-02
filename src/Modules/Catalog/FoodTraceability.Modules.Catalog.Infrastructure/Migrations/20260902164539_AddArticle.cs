using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodTraceability.Modules.Catalog.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddArticle : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "article",
            schema: "catalog",
            columns: table => new
            {
                article_id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                product_id = table.Column<Guid>(type: "uuid", nullable: false),
                article_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                gtin = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_article", x => x.article_id);
                table.UniqueConstraint("ak_article_article_id_organization_id", x => new { x.article_id, x.organization_id });
                table.CheckConstraint("ck_article_gtin_format", "gtin IS NULL OR (gtin ~ '^[0-9]+$' AND length(gtin) IN (8, 12, 13, 14))");
                table.ForeignKey(
                    name: "fk_article_product_product_id",
                    column: x => x.product_id,
                    principalSchema: "catalog",
                    principalTable: "product",
                    principalColumn: "product_id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.AddForeignKey(
            name: "fk_article_org_organization",
            schema: "catalog",
            table: "article",
            column: "organization_id",
            principalSchema: "org",
            principalTable: "organization",
            principalColumn: "organization_id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.CreateIndex(
            name: "ix_article_organization_id_gtin",
            schema: "catalog",
            table: "article",
            columns: new[] { "organization_id", "gtin" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_article_product_id",
            schema: "catalog",
            table: "article",
            column: "product_id");

        migrationBuilder.Sql(
            """
            CREATE UNIQUE INDEX ux_article_organization_id_article_number_upper
                ON catalog.article (organization_id, upper(article_number));
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "article",
            schema: "catalog");
    }
}

