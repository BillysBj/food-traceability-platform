using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FoodTraceability.Modules.Traceability.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddEventType : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "event_type",
            schema: "trace",
            columns: table => new
            {
                event_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_event_type", x => x.event_type_id);
            });

        migrationBuilder.InsertData(
            schema: "trace",
            table: "event_type",
            columns: new[] { "event_type_id", "code", "created_at" },
            values: new object[,]
            {
                    { new Guid("0195e858-b3f0-54be-ad01-581f168ecf8f"), "RECEIVE", new DateTimeOffset(new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("0283c126-a16a-516d-aeec-b246af44b88a"), "UNBLOCK", new DateTimeOffset(new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("243a920d-b978-5202-896e-6428a74a2c22"), "DELIVER", new DateTimeOffset(new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("26614721-e8f4-5513-a9cf-0c2422fc003b"), "STORE", new DateTimeOffset(new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("3022cd5f-94b0-52d8-af19-2f1959360602"), "SELL", new DateTimeOffset(new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("36ca16f4-df60-56f3-8d30-60af75da9c92"), "DISPOSE", new DateTimeOffset(new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("49163869-4ef0-501f-984d-ab3adb5e1996"), "SAMPLE", new DateTimeOffset(new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("4e471a84-b9d6-5618-aee9-6ca1f3373ddd"), "TRANSFER", new DateTimeOffset(new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("5373e85b-e968-55b7-a8bc-7f44008647d2"), "SPLIT", new DateTimeOffset(new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("58929c0a-8d65-5a38-a6d1-6d74cb144b44"), "MIX", new DateTimeOffset(new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("76ad5624-efd3-54d8-903e-7a9fdb221adb"), "BOTTLE", new DateTimeOffset(new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("784ded8c-54a9-595e-b2b2-10acbf16e887"), "HARVEST", new DateTimeOffset(new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("7a5bb899-9c04-5dbf-9341-64bc402c2ed3"), "BLOCK", new DateTimeOffset(new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("7b93d110-a9e4-53af-b3fc-5f7580f7a8f6"), "SHIP", new DateTimeOffset(new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("99147ae3-6e5b-599d-830d-3f55d80d8242"), "PROCESS", new DateTimeOffset(new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("ad863244-bf75-5f34-886a-eb400f82985d"), "RETURN", new DateTimeOffset(new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("b2830815-b10e-5507-9ed3-13679fc08e5e"), "PRESS", new DateTimeOffset(new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("b94a7503-17b1-506a-a738-b169290da379"), "PACK", new DateTimeOffset(new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("dbe268c4-4290-5249-b3ad-5ffe81b3fdc8"), "QUALITY_RELEASE", new DateTimeOffset(new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
            });

        migrationBuilder.CreateIndex(
            name: "ix_event_type_code",
            schema: "trace",
            table: "event_type",
            column: "code",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "event_type",
            schema: "trace");
    }
}
