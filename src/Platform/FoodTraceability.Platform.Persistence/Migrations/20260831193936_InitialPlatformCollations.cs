using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodTraceability.Platform.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialPlatformCollations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:CollationDefinition:el", "el-GR,el-GR,icu,True")
            .Annotation("Npgsql:CollationDefinition:en", "en-US,en-US,icu,True");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {

    }
}
