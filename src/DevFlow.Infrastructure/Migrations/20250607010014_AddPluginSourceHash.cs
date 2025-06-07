using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPluginSourceHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceHash",
                table: "Plugins",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_SourceHash",
                table: "Plugins",
                column: "SourceHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Plugins_SourceHash",
                table: "Plugins");

            migrationBuilder.DropColumn(
                name: "SourceHash",
                table: "Plugins");
        }
    }
}
