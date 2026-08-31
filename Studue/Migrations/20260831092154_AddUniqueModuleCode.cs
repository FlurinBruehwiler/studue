using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudueSharp.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueModuleCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Modules_Code",
                table: "Modules",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Modules_Code",
                table: "Modules");
        }
    }
}
