using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudueSharp.Migrations
{
    /// <inheritdoc />
    public partial class AddWriteToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WriteToken",
                table: "Students",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WriteToken",
                table: "Students");
        }
    }
}
