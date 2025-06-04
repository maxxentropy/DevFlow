using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixPluginDependenciesJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // First, update any existing null or empty Dependencies to valid JSON
            migrationBuilder.Sql(
                "UPDATE Plugins SET Dependencies = '[]' WHERE Dependencies IS NULL OR Dependencies = '' OR LENGTH(TRIM(Dependencies)) = 0;");
            
            migrationBuilder.AlterColumn<string>(
                name: "Dependencies",
                table: "Plugins",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "'[]'",
                oldClrType: typeof(string),
                oldType: "TEXT");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Dependencies",
                table: "Plugins",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldDefaultValueSql: "'[]'");
        }
    }
}
