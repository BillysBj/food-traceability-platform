using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodTraceability.Modules.Traceability.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddLotArticleAndQuantity : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The columns are added as NOT NULL without a default. There is no lot data to
        // migrate, so no backfill and no temporarily nullable column is needed. A default
        // would also be wrong here: an empty article or unit id satisfies no foreign key,
        // and a zero quantity violates ck_lot_quantity_positive.
        migrationBuilder.AddColumn<Guid>(
            name: "article_id",
            schema: "trace",
            table: "lot",
            type: "uuid",
            nullable: false);

        migrationBuilder.AddColumn<decimal>(
            name: "quantity",
            schema: "trace",
            table: "lot",
            type: "numeric(18,6)",
            precision: 18,
            scale: 6,
            nullable: false);

        migrationBuilder.AddColumn<Guid>(
            name: "unit_id",
            schema: "trace",
            table: "lot",
            type: "uuid",
            nullable: false);

        migrationBuilder.AddCheckConstraint(
            name: "ck_lot_quantity_positive",
            schema: "trace",
            table: "lot",
            sql: "quantity > 0");

        // The composite foreign key targets ak_article_article_id_organization_id in
        // catalog.article. It makes the database reject a lot that references an article
        // of another organization, structurally instead of by an application check.
        // Both foreign keys are declared here rather than in the EF model, because the
        // Traceability module must not reference the Catalog module (D-11).
        migrationBuilder.AddForeignKey(
            name: "fk_lot_catalog_article",
            schema: "trace",
            table: "lot",
            columns: ["article_id", "organization_id"],
            principalSchema: "catalog",
            principalTable: "article",
            principalColumns: ["article_id", "organization_id"],
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "fk_lot_catalog_unit",
            schema: "trace",
            table: "lot",
            column: "unit_id",
            principalSchema: "catalog",
            principalTable: "unit",
            principalColumn: "unit_id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_lot_catalog_unit",
            schema: "trace",
            table: "lot");

        migrationBuilder.DropForeignKey(
            name: "fk_lot_catalog_article",
            schema: "trace",
            table: "lot");

        migrationBuilder.DropCheckConstraint(
            name: "ck_lot_quantity_positive",
            schema: "trace",
            table: "lot");

        migrationBuilder.DropColumn(
            name: "article_id",
            schema: "trace",
            table: "lot");

        migrationBuilder.DropColumn(
            name: "quantity",
            schema: "trace",
            table: "lot");

        migrationBuilder.DropColumn(
            name: "unit_id",
            schema: "trace",
            table: "lot");
    }
}
