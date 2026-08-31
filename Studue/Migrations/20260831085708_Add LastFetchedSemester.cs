using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudueSharp.Migrations
{
    /// <inheritdoc />
    public partial class AddLastFetchedSemester : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastFetchedSemester",
                table: "Students",
                type: "TEXT",
                nullable: false,
                defaultValue: "FS2026");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastFetchedSemester",
                table: "Students");
        }
    }
}
