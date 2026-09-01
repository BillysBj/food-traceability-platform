using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodTraceability.Modules.Identity.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddUserCredentialsAndRefreshTokens : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "refresh_token",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                session_id = table.Column<Guid>(type: "uuid", nullable: false),
                token_hash = table.Column<string>(type: "text", nullable: false),
                issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_refresh_token", x => x.id);
                table.CheckConstraint("ck_refresh_token_expires_after_issued", "expires_at > issued_at");
                table.CheckConstraint("ck_refresh_token_revoked_not_before_issued", "revoked_at IS NULL OR revoked_at >= issued_at");
                table.ForeignKey(
                    name: "fk_refresh_token_user_user_id",
                    column: x => x.user_id,
                    principalSchema: "identity",
                    principalTable: "user",
                    principalColumn: "user_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "user_credential",
            schema: "identity",
            columns: table => new
            {
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                password_hash = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_user_credential", x => x.user_id);
                table.ForeignKey(
                    name: "fk_user_credential_user_user_id",
                    column: x => x.user_id,
                    principalSchema: "identity",
                    principalTable: "user",
                    principalColumn: "user_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_refresh_token_session_id",
            schema: "identity",
            table: "refresh_token",
            column: "session_id");

        migrationBuilder.CreateIndex(
            name: "ix_refresh_token_token_hash",
            schema: "identity",
            table: "refresh_token",
            column: "token_hash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_refresh_token_user_id",
            schema: "identity",
            table: "refresh_token",
            column: "user_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "refresh_token",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "user_credential",
            schema: "identity");
    }
}
