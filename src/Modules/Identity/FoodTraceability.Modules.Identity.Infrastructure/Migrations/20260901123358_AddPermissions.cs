using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FoodTraceability.Modules.Identity.Infrastructure.Migrations;

    /// <inheritdoc />
    public partial class AddPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "permission",
                schema: "identity",
                columns: table => new
                {
                    permission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permission", x => x.permission_id);
                });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "permission",
                columns: new[] { "permission_id", "code", "description" },
                values: new object[,]
                {
                    { new Guid("06de594f-5714-5198-b877-5b84fdb8a1bc"), "product.update", null },
                    { new Guid("123005cc-2642-5a72-b2d8-f3f5d989bd32"), "quality.sample.create", null },
                    { new Guid("13fae5e7-0a2d-5b72-9401-3c9e398b2b55"), "product.read", null },
                    { new Guid("1ed56bb0-2b7b-5c76-a63a-5072027ee5bf"), "trace.read", null },
                    { new Guid("227a9dd3-048c-51e8-aae9-c0268a185640"), "user.manage", null },
                    { new Guid("27c75590-f8fe-5fbe-afc4-190fcca52776"), "delivery.read", null },
                    { new Guid("51d975f4-e516-525c-a24b-b94fd8cfdea1"), "role.read", null },
                    { new Guid("5239d89e-16e3-586b-9996-32085b6b867d"), "organization.read", null },
                    { new Guid("5362b9ec-0f34-51f9-9423-ec9940ec8e22"), "permission.read", null },
                    { new Guid("542cd3e3-27c8-5a3d-b634-10cff370a922"), "user.read", null },
                    { new Guid("573fe7e9-c8f3-5b46-a885-3f2ae13ca228"), "audit.read", null },
                    { new Guid("78562e51-4c55-5309-94b3-c96ef465ad9b"), "document.upload", null },
                    { new Guid("87bbc734-d0ce-544b-ad62-ddefb7f56a80"), "quality.release", null },
                    { new Guid("8d153853-ab36-5e34-8537-2e05556feeee"), "lot.read", null },
                    { new Guid("a131fb8f-4ffb-510e-8aa3-2d7fa78c3383"), "lot.update", null },
                    { new Guid("ad1e5f46-77ad-5029-85b1-5048d78c6f8d"), "document.read", null },
                    { new Guid("b0f144ee-b4da-5e04-9d5f-253a24922db2"), "transport.create", null },
                    { new Guid("b9c07ec4-7da3-570b-b279-57f758496798"), "organization.manage", null },
                    { new Guid("bad85ec4-523c-5505-b4aa-933cd062add9"), "quality.result.create", null },
                    { new Guid("becb86f7-1a71-5ccb-b0ab-3aade89e6177"), "product.create", null },
                    { new Guid("d8f29f61-9bbb-5659-8b88-559242e75918"), "quality.read", null },
                    { new Guid("d8f57584-4487-5fa8-8a59-5e7effd62f06"), "delivery.create", null },
                    { new Guid("da54dd5a-9013-5c1b-8b4e-fdc30258e02f"), "lot.create", null },
                    { new Guid("e577d2b4-c89d-58db-a0f7-5be7f040734a"), "quality.block", null },
                    { new Guid("e6708b10-357c-5285-a292-69deae973983"), "transport.read", null },
                    { new Guid("f999fb2f-ee88-5dd9-a4ec-9170587f7d9b"), "trace.event.create", null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_permission_code",
                schema: "identity",
                table: "permission",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "permission",
                schema: "identity");
        }
}
