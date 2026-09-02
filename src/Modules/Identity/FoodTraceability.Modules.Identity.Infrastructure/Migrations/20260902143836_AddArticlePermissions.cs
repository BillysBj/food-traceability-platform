using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FoodTraceability.Modules.Identity.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddArticlePermissions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.InsertData(
            schema: "identity",
            table: "permission",
            columns: new[] { "permission_id", "code", "description" },
            values: new object[,]
            {
                    { new Guid("953dda24-0a71-57bc-b06c-dfc895a1fae2"), "article.update", null },
                    { new Guid("b2d81829-2d1c-5e6a-9b19-7d275f3aa0cf"), "article.create", null },
                    { new Guid("f57cf66c-3591-54bd-a12e-5f53f141c48e"), "article.read", null }
            });

        migrationBuilder.InsertData(
            schema: "identity",
            table: "role_permission",
            columns: new[] { "permission_id", "role_id" },
            values: new object[,]
            {
                    { new Guid("f57cf66c-3591-54bd-a12e-5f53f141c48e"), new Guid("00ec29aa-1bc7-540e-b04d-02c3497f50b3") },
                    { new Guid("953dda24-0a71-57bc-b06c-dfc895a1fae2"), new Guid("34644a1b-9bbb-5005-98a1-b3584dd8bf69") },
                    { new Guid("b2d81829-2d1c-5e6a-9b19-7d275f3aa0cf"), new Guid("34644a1b-9bbb-5005-98a1-b3584dd8bf69") },
                    { new Guid("f57cf66c-3591-54bd-a12e-5f53f141c48e"), new Guid("34644a1b-9bbb-5005-98a1-b3584dd8bf69") },
                    { new Guid("953dda24-0a71-57bc-b06c-dfc895a1fae2"), new Guid("351aaedc-26d7-5406-9109-f4f8139ec1a8") },
                    { new Guid("b2d81829-2d1c-5e6a-9b19-7d275f3aa0cf"), new Guid("351aaedc-26d7-5406-9109-f4f8139ec1a8") },
                    { new Guid("f57cf66c-3591-54bd-a12e-5f53f141c48e"), new Guid("351aaedc-26d7-5406-9109-f4f8139ec1a8") },
                    { new Guid("953dda24-0a71-57bc-b06c-dfc895a1fae2"), new Guid("d8c0f985-5ce5-59b9-bf3b-71ae6bc5616a") },
                    { new Guid("b2d81829-2d1c-5e6a-9b19-7d275f3aa0cf"), new Guid("d8c0f985-5ce5-59b9-bf3b-71ae6bc5616a") },
                    { new Guid("f57cf66c-3591-54bd-a12e-5f53f141c48e"), new Guid("d8c0f985-5ce5-59b9-bf3b-71ae6bc5616a") }
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(
            schema: "identity",
            table: "role_permission",
            keyColumns: new[] { "permission_id", "role_id" },
            keyValues: new object[] { new Guid("f57cf66c-3591-54bd-a12e-5f53f141c48e"), new Guid("00ec29aa-1bc7-540e-b04d-02c3497f50b3") });

        migrationBuilder.DeleteData(
            schema: "identity",
            table: "role_permission",
            keyColumns: new[] { "permission_id", "role_id" },
            keyValues: new object[] { new Guid("953dda24-0a71-57bc-b06c-dfc895a1fae2"), new Guid("34644a1b-9bbb-5005-98a1-b3584dd8bf69") });

        migrationBuilder.DeleteData(
            schema: "identity",
            table: "role_permission",
            keyColumns: new[] { "permission_id", "role_id" },
            keyValues: new object[] { new Guid("b2d81829-2d1c-5e6a-9b19-7d275f3aa0cf"), new Guid("34644a1b-9bbb-5005-98a1-b3584dd8bf69") });

        migrationBuilder.DeleteData(
            schema: "identity",
            table: "role_permission",
            keyColumns: new[] { "permission_id", "role_id" },
            keyValues: new object[] { new Guid("f57cf66c-3591-54bd-a12e-5f53f141c48e"), new Guid("34644a1b-9bbb-5005-98a1-b3584dd8bf69") });

        migrationBuilder.DeleteData(
            schema: "identity",
            table: "role_permission",
            keyColumns: new[] { "permission_id", "role_id" },
            keyValues: new object[] { new Guid("953dda24-0a71-57bc-b06c-dfc895a1fae2"), new Guid("351aaedc-26d7-5406-9109-f4f8139ec1a8") });

        migrationBuilder.DeleteData(
            schema: "identity",
            table: "role_permission",
            keyColumns: new[] { "permission_id", "role_id" },
            keyValues: new object[] { new Guid("b2d81829-2d1c-5e6a-9b19-7d275f3aa0cf"), new Guid("351aaedc-26d7-5406-9109-f4f8139ec1a8") });

        migrationBuilder.DeleteData(
            schema: "identity",
            table: "role_permission",
            keyColumns: new[] { "permission_id", "role_id" },
            keyValues: new object[] { new Guid("f57cf66c-3591-54bd-a12e-5f53f141c48e"), new Guid("351aaedc-26d7-5406-9109-f4f8139ec1a8") });

        migrationBuilder.DeleteData(
            schema: "identity",
            table: "role_permission",
            keyColumns: new[] { "permission_id", "role_id" },
            keyValues: new object[] { new Guid("953dda24-0a71-57bc-b06c-dfc895a1fae2"), new Guid("d8c0f985-5ce5-59b9-bf3b-71ae6bc5616a") });

        migrationBuilder.DeleteData(
            schema: "identity",
            table: "role_permission",
            keyColumns: new[] { "permission_id", "role_id" },
            keyValues: new object[] { new Guid("b2d81829-2d1c-5e6a-9b19-7d275f3aa0cf"), new Guid("d8c0f985-5ce5-59b9-bf3b-71ae6bc5616a") });

        migrationBuilder.DeleteData(
            schema: "identity",
            table: "role_permission",
            keyColumns: new[] { "permission_id", "role_id" },
            keyValues: new object[] { new Guid("f57cf66c-3591-54bd-a12e-5f53f141c48e"), new Guid("d8c0f985-5ce5-59b9-bf3b-71ae6bc5616a") });

        migrationBuilder.DeleteData(
            schema: "identity",
            table: "permission",
            keyColumn: "permission_id",
            keyValue: new Guid("953dda24-0a71-57bc-b06c-dfc895a1fae2"));

        migrationBuilder.DeleteData(
            schema: "identity",
            table: "permission",
            keyColumn: "permission_id",
            keyValue: new Guid("b2d81829-2d1c-5e6a-9b19-7d275f3aa0cf"));

        migrationBuilder.DeleteData(
            schema: "identity",
            table: "permission",
            keyColumn: "permission_id",
            keyValue: new Guid("f57cf66c-3591-54bd-a12e-5f53f141c48e"));
    }
}
