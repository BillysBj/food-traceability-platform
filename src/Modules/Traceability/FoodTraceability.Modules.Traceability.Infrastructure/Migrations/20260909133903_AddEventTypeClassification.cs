using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodTraceability.Modules.Traceability.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddEventTypeClassification : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The nineteen seeded rows already exist. Backfill them before enforcing
        // NOT NULL, without leaving an invalid or implicitly permissive default.
        migrationBuilder.AddColumn<string>(
            name: "classification",
            schema: "trace",
            table: "event_type",
            type: "character varying(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.UpdateData(
            schema: "trace",
            table: "event_type",
            keyColumn: "event_type_id",
            keyValue: new Guid("0195e858-b3f0-54be-ad01-581f168ecf8f"),
            column: "classification",
            value: "TRACEABILITY_AWAITING_LOGISTICS");

        migrationBuilder.UpdateData(
            schema: "trace",
            table: "event_type",
            keyColumn: "event_type_id",
            keyValue: new Guid("0283c126-a16a-516d-aeec-b246af44b88a"),
            column: "classification",
            value: "QUALITY");

        migrationBuilder.UpdateData(
            schema: "trace",
            table: "event_type",
            keyColumn: "event_type_id",
            keyValue: new Guid("243a920d-b978-5202-896e-6428a74a2c22"),
            column: "classification",
            value: "LOGISTICS");

        migrationBuilder.UpdateData(
            schema: "trace",
            table: "event_type",
            keyColumn: "event_type_id",
            keyValue: new Guid("26614721-e8f4-5513-a9cf-0c2422fc003b"),
            column: "classification",
            value: "DEFERRED");

        migrationBuilder.UpdateData(
            schema: "trace",
            table: "event_type",
            keyColumn: "event_type_id",
            keyValue: new Guid("3022cd5f-94b0-52d8-af19-2f1959360602"),
            column: "classification",
            value: "TRACEABILITY_AWAITING_LOGISTICS");

        migrationBuilder.UpdateData(
            schema: "trace",
            table: "event_type",
            keyColumn: "event_type_id",
            keyValue: new Guid("36ca16f4-df60-56f3-8d30-60af75da9c92"),
            column: "classification",
            value: "TRACEABILITY");

        migrationBuilder.UpdateData(
            schema: "trace",
            table: "event_type",
            keyColumn: "event_type_id",
            keyValue: new Guid("49163869-4ef0-501f-984d-ab3adb5e1996"),
            column: "classification",
            value: "TRACEABILITY");

        migrationBuilder.UpdateData(
            schema: "trace",
            table: "event_type",
            keyColumn: "event_type_id",
            keyValue: new Guid("4e471a84-b9d6-5618-aee9-6ca1f3373ddd"),
            column: "classification",
            value: "LOGISTICS");

        migrationBuilder.UpdateData(
            schema: "trace",
            table: "event_type",
            keyColumn: "event_type_id",
            keyValue: new Guid("5373e85b-e968-55b7-a8bc-7f44008647d2"),
            column: "classification",
            value: "TRACEABILITY");

        migrationBuilder.UpdateData(
            schema: "trace",
            table: "event_type",
            keyColumn: "event_type_id",
            keyValue: new Guid("58929c0a-8d65-5a38-a6d1-6d74cb144b44"),
            column: "classification",
            value: "TRACEABILITY");

        migrationBuilder.UpdateData(
            schema: "trace",
            table: "event_type",
            keyColumn: "event_type_id",
            keyValue: new Guid("76ad5624-efd3-54d8-903e-7a9fdb221adb"),
            column: "classification",
            value: "TRACEABILITY");

        migrationBuilder.UpdateData(
            schema: "trace",
            table: "event_type",
            keyColumn: "event_type_id",
            keyValue: new Guid("784ded8c-54a9-595e-b2b2-10acbf16e887"),
            column: "classification",
            value: "TRACEABILITY");

        migrationBuilder.UpdateData(
            schema: "trace",
            table: "event_type",
            keyColumn: "event_type_id",
            keyValue: new Guid("7a5bb899-9c04-5dbf-9341-64bc402c2ed3"),
            column: "classification",
            value: "QUALITY");

        migrationBuilder.UpdateData(
            schema: "trace",
            table: "event_type",
            keyColumn: "event_type_id",
            keyValue: new Guid("7b93d110-a9e4-53af-b3fc-5f7580f7a8f6"),
            column: "classification",
            value: "LOGISTICS");

        migrationBuilder.UpdateData(
            schema: "trace",
            table: "event_type",
            keyColumn: "event_type_id",
            keyValue: new Guid("99147ae3-6e5b-599d-830d-3f55d80d8242"),
            column: "classification",
            value: "TRACEABILITY");

        migrationBuilder.UpdateData(
            schema: "trace",
            table: "event_type",
            keyColumn: "event_type_id",
            keyValue: new Guid("ad863244-bf75-5f34-886a-eb400f82985d"),
            column: "classification",
            value: "TRACEABILITY_AWAITING_LOGISTICS");

        migrationBuilder.UpdateData(
            schema: "trace",
            table: "event_type",
            keyColumn: "event_type_id",
            keyValue: new Guid("b2830815-b10e-5507-9ed3-13679fc08e5e"),
            column: "classification",
            value: "TRACEABILITY");

        migrationBuilder.UpdateData(
            schema: "trace",
            table: "event_type",
            keyColumn: "event_type_id",
            keyValue: new Guid("b94a7503-17b1-506a-a738-b169290da379"),
            column: "classification",
            value: "DEFERRED");

        migrationBuilder.UpdateData(
            schema: "trace",
            table: "event_type",
            keyColumn: "event_type_id",
            keyValue: new Guid("dbe268c4-4290-5249-b3ad-5ffe81b3fdc8"),
            column: "classification",
            value: "QUALITY");

        migrationBuilder.AlterColumn<string>(
            name: "classification",
            schema: "trace",
            table: "event_type",
            type: "character varying(32)",
            maxLength: 32,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(32)",
            oldMaxLength: 32,
            oldNullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_event_type_classification",
            schema: "trace",
            table: "event_type",
            sql: "classification IN ('TRACEABILITY', 'TRACEABILITY_AWAITING_LOGISTICS', 'LOGISTICS', 'QUALITY', 'DEFERRED')");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_event_type_classification",
            schema: "trace",
            table: "event_type");

        migrationBuilder.DropColumn(
            name: "classification",
            schema: "trace",
            table: "event_type");
    }
}
