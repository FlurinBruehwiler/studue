using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudueSharp.Migrations
{
    /// <inheritdoc />
    public partial class AddIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Students_StudentId",
                table: "Students",
                column: "StudentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Students_StudentId",
                table: "Students");
        }
    }
}
