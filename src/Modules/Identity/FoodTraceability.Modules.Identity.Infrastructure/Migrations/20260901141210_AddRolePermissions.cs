using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FoodTraceability.Modules.Identity.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddRolePermissions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
                name: "role_permission",
                schema: "identity",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_permission", x => new { x.role_id, x.permission_id });
                    table.ForeignKey(
                        name: "fk_role_permission_permission_permission_id",
                        column: x => x.permission_id,
                        principalSchema: "identity",
                        principalTable: "permission",
                        principalColumn: "permission_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_role_permission_role_role_id",
                        column: x => x.role_id,
                        principalSchema: "identity",
                        principalTable: "role",
                        principalColumn: "role_id",
                        onDelete: ReferentialAction.Restrict);
                });

        migrationBuilder.InsertData(
                schema: "identity",
                table: "role_permission",
                columns: new[] { "permission_id", "role_id" },
                values: new object[,]
                {
                    { new Guid("1ed56bb0-2b7b-5c76-a63a-5072027ee5bf"), new Guid("0002868f-4330-5c7c-aac4-77420d2aff52") },
                    { new Guid("27c75590-f8fe-5fbe-afc4-190fcca52776"), new Guid("0002868f-4330-5c7c-aac4-77420d2aff52") },
                    { new Guid("8d153853-ab36-5e34-8537-2e05556feeee"), new Guid("0002868f-4330-5c7c-aac4-77420d2aff52") },
                    { new Guid("ad1e5f46-77ad-5029-85b1-5048d78c6f8d"), new Guid("0002868f-4330-5c7c-aac4-77420d2aff52") },
                    { new Guid("227a9dd3-048c-51e8-aae9-c0268a185640"), new Guid("00ec29aa-1bc7-540e-b04d-02c3497f50b3") },
                    { new Guid("51d975f4-e516-525c-a24b-b94fd8cfdea1"), new Guid("00ec29aa-1bc7-540e-b04d-02c3497f50b3") },
                    { new Guid("5239d89e-16e3-586b-9996-32085b6b867d"), new Guid("00ec29aa-1bc7-540e-b04d-02c3497f50b3") },
                    { new Guid("542cd3e3-27c8-5a3d-b634-10cff370a922"), new Guid("00ec29aa-1bc7-540e-b04d-02c3497f50b3") },
                    { new Guid("b9c07ec4-7da3-570b-b279-57f758496798"), new Guid("00ec29aa-1bc7-540e-b04d-02c3497f50b3") },
                    { new Guid("1ed56bb0-2b7b-5c76-a63a-5072027ee5bf"), new Guid("222d9d3e-a711-5607-820a-59c9f497bbaf") },
                    { new Guid("27c75590-f8fe-5fbe-afc4-190fcca52776"), new Guid("222d9d3e-a711-5607-820a-59c9f497bbaf") },
                    { new Guid("78562e51-4c55-5309-94b3-c96ef465ad9b"), new Guid("222d9d3e-a711-5607-820a-59c9f497bbaf") },
                    { new Guid("8d153853-ab36-5e34-8537-2e05556feeee"), new Guid("222d9d3e-a711-5607-820a-59c9f497bbaf") },
                    { new Guid("ad1e5f46-77ad-5029-85b1-5048d78c6f8d"), new Guid("222d9d3e-a711-5607-820a-59c9f497bbaf") },
                    { new Guid("b0f144ee-b4da-5e04-9d5f-253a24922db2"), new Guid("222d9d3e-a711-5607-820a-59c9f497bbaf") },
                    { new Guid("d8f57584-4487-5fa8-8a59-5e7effd62f06"), new Guid("222d9d3e-a711-5607-820a-59c9f497bbaf") },
                    { new Guid("e6708b10-357c-5285-a292-69deae973983"), new Guid("222d9d3e-a711-5607-820a-59c9f497bbaf") },
                    { new Guid("78562e51-4c55-5309-94b3-c96ef465ad9b"), new Guid("2ef0f055-298c-512e-a35e-10d180133f51") },
                    { new Guid("8d153853-ab36-5e34-8537-2e05556feeee"), new Guid("2ef0f055-298c-512e-a35e-10d180133f51") },
                    { new Guid("ad1e5f46-77ad-5029-85b1-5048d78c6f8d"), new Guid("2ef0f055-298c-512e-a35e-10d180133f51") },
                    { new Guid("bad85ec4-523c-5505-b4aa-933cd062add9"), new Guid("2ef0f055-298c-512e-a35e-10d180133f51") },
                    { new Guid("d8f29f61-9bbb-5659-8b88-559242e75918"), new Guid("2ef0f055-298c-512e-a35e-10d180133f51") },
                    { new Guid("13fae5e7-0a2d-5b72-9401-3c9e398b2b55"), new Guid("34644a1b-9bbb-5005-98a1-b3584dd8bf69") },
                    { new Guid("1ed56bb0-2b7b-5c76-a63a-5072027ee5bf"), new Guid("34644a1b-9bbb-5005-98a1-b3584dd8bf69") },
                    { new Guid("78562e51-4c55-5309-94b3-c96ef465ad9b"), new Guid("34644a1b-9bbb-5005-98a1-b3584dd8bf69") },
                    { new Guid("8d153853-ab36-5e34-8537-2e05556feeee"), new Guid("34644a1b-9bbb-5005-98a1-b3584dd8bf69") },
                    { new Guid("a131fb8f-4ffb-510e-8aa3-2d7fa78c3383"), new Guid("34644a1b-9bbb-5005-98a1-b3584dd8bf69") },
                    { new Guid("ad1e5f46-77ad-5029-85b1-5048d78c6f8d"), new Guid("34644a1b-9bbb-5005-98a1-b3584dd8bf69") },
                    { new Guid("da54dd5a-9013-5c1b-8b4e-fdc30258e02f"), new Guid("34644a1b-9bbb-5005-98a1-b3584dd8bf69") },
                    { new Guid("f999fb2f-ee88-5dd9-a4ec-9170587f7d9b"), new Guid("34644a1b-9bbb-5005-98a1-b3584dd8bf69") },
                    { new Guid("13fae5e7-0a2d-5b72-9401-3c9e398b2b55"), new Guid("351aaedc-26d7-5406-9109-f4f8139ec1a8") },
                    { new Guid("1ed56bb0-2b7b-5c76-a63a-5072027ee5bf"), new Guid("351aaedc-26d7-5406-9109-f4f8139ec1a8") },
                    { new Guid("78562e51-4c55-5309-94b3-c96ef465ad9b"), new Guid("351aaedc-26d7-5406-9109-f4f8139ec1a8") },
                    { new Guid("8d153853-ab36-5e34-8537-2e05556feeee"), new Guid("351aaedc-26d7-5406-9109-f4f8139ec1a8") },
                    { new Guid("a131fb8f-4ffb-510e-8aa3-2d7fa78c3383"), new Guid("351aaedc-26d7-5406-9109-f4f8139ec1a8") },
                    { new Guid("ad1e5f46-77ad-5029-85b1-5048d78c6f8d"), new Guid("351aaedc-26d7-5406-9109-f4f8139ec1a8") },
                    { new Guid("da54dd5a-9013-5c1b-8b4e-fdc30258e02f"), new Guid("351aaedc-26d7-5406-9109-f4f8139ec1a8") },
                    { new Guid("f999fb2f-ee88-5dd9-a4ec-9170587f7d9b"), new Guid("351aaedc-26d7-5406-9109-f4f8139ec1a8") },
                    { new Guid("123005cc-2642-5a72-b2d8-f3f5d989bd32"), new Guid("38623612-6286-55ce-9af2-ea523f760be3") },
                    { new Guid("1ed56bb0-2b7b-5c76-a63a-5072027ee5bf"), new Guid("38623612-6286-55ce-9af2-ea523f760be3") },
                    { new Guid("78562e51-4c55-5309-94b3-c96ef465ad9b"), new Guid("38623612-6286-55ce-9af2-ea523f760be3") },
                    { new Guid("87bbc734-d0ce-544b-ad62-ddefb7f56a80"), new Guid("38623612-6286-55ce-9af2-ea523f760be3") },
                    { new Guid("8d153853-ab36-5e34-8537-2e05556feeee"), new Guid("38623612-6286-55ce-9af2-ea523f760be3") },
                    { new Guid("ad1e5f46-77ad-5029-85b1-5048d78c6f8d"), new Guid("38623612-6286-55ce-9af2-ea523f760be3") },
                    { new Guid("d8f29f61-9bbb-5659-8b88-559242e75918"), new Guid("38623612-6286-55ce-9af2-ea523f760be3") },
                    { new Guid("e577d2b4-c89d-58db-a0f7-5be7f040734a"), new Guid("38623612-6286-55ce-9af2-ea523f760be3") },
                    { new Guid("1ed56bb0-2b7b-5c76-a63a-5072027ee5bf"), new Guid("a187d055-fa04-56ef-b488-2b6ad6216007") },
                    { new Guid("573fe7e9-c8f3-5b46-a885-3f2ae13ca228"), new Guid("a187d055-fa04-56ef-b488-2b6ad6216007") },
                    { new Guid("8d153853-ab36-5e34-8537-2e05556feeee"), new Guid("a187d055-fa04-56ef-b488-2b6ad6216007") },
                    { new Guid("ad1e5f46-77ad-5029-85b1-5048d78c6f8d"), new Guid("a187d055-fa04-56ef-b488-2b6ad6216007") },
                    { new Guid("d8f29f61-9bbb-5659-8b88-559242e75918"), new Guid("a187d055-fa04-56ef-b488-2b6ad6216007") },
                    { new Guid("13fae5e7-0a2d-5b72-9401-3c9e398b2b55"), new Guid("d8c0f985-5ce5-59b9-bf3b-71ae6bc5616a") },
                    { new Guid("1ed56bb0-2b7b-5c76-a63a-5072027ee5bf"), new Guid("d8c0f985-5ce5-59b9-bf3b-71ae6bc5616a") },
                    { new Guid("78562e51-4c55-5309-94b3-c96ef465ad9b"), new Guid("d8c0f985-5ce5-59b9-bf3b-71ae6bc5616a") },
                    { new Guid("8d153853-ab36-5e34-8537-2e05556feeee"), new Guid("d8c0f985-5ce5-59b9-bf3b-71ae6bc5616a") },
                    { new Guid("a131fb8f-4ffb-510e-8aa3-2d7fa78c3383"), new Guid("d8c0f985-5ce5-59b9-bf3b-71ae6bc5616a") },
                    { new Guid("ad1e5f46-77ad-5029-85b1-5048d78c6f8d"), new Guid("d8c0f985-5ce5-59b9-bf3b-71ae6bc5616a") },
                    { new Guid("da54dd5a-9013-5c1b-8b4e-fdc30258e02f"), new Guid("d8c0f985-5ce5-59b9-bf3b-71ae6bc5616a") },
                    { new Guid("f999fb2f-ee88-5dd9-a4ec-9170587f7d9b"), new Guid("d8c0f985-5ce5-59b9-bf3b-71ae6bc5616a") },
                    { new Guid("06de594f-5714-5198-b877-5b84fdb8a1bc"), new Guid("ec72b8b5-2610-5efd-aa7f-6aa59889da7d") },
                    { new Guid("13fae5e7-0a2d-5b72-9401-3c9e398b2b55"), new Guid("ec72b8b5-2610-5efd-aa7f-6aa59889da7d") },
                    { new Guid("227a9dd3-048c-51e8-aae9-c0268a185640"), new Guid("ec72b8b5-2610-5efd-aa7f-6aa59889da7d") },
                    { new Guid("51d975f4-e516-525c-a24b-b94fd8cfdea1"), new Guid("ec72b8b5-2610-5efd-aa7f-6aa59889da7d") },
                    { new Guid("5239d89e-16e3-586b-9996-32085b6b867d"), new Guid("ec72b8b5-2610-5efd-aa7f-6aa59889da7d") },
                    { new Guid("5362b9ec-0f34-51f9-9423-ec9940ec8e22"), new Guid("ec72b8b5-2610-5efd-aa7f-6aa59889da7d") },
                    { new Guid("542cd3e3-27c8-5a3d-b634-10cff370a922"), new Guid("ec72b8b5-2610-5efd-aa7f-6aa59889da7d") },
                    { new Guid("b9c07ec4-7da3-570b-b279-57f758496798"), new Guid("ec72b8b5-2610-5efd-aa7f-6aa59889da7d") },
                    { new Guid("becb86f7-1a71-5ccb-b0ab-3aade89e6177"), new Guid("ec72b8b5-2610-5efd-aa7f-6aa59889da7d") }
                });

        migrationBuilder.CreateIndex(
                name: "ix_role_permission_permission_id",
                schema: "identity",
                table: "role_permission",
                column: "permission_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "role_permission",
            schema: "identity");
    }
}
