using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudueSharp.Migrations
{
    /// <inheritdoc />
    public partial class LinkStudentsToScheduleEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Students_ScheduleEntries_ScheduleEntryId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_ScheduleEntryId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "ScheduleEntryId",
                table: "Students");

            migrationBuilder.CreateTable(
                name: "ScheduleEntryStudent",
                columns: table => new
                {
                    ScheduleEntriesId = table.Column<int>(type: "INTEGER", nullable: false),
                    StudentsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleEntryStudent", x => new { x.ScheduleEntriesId, x.StudentsId });
                    table.ForeignKey(
                        name: "FK_ScheduleEntryStudent_ScheduleEntries_ScheduleEntriesId",
                        column: x => x.ScheduleEntriesId,
                        principalTable: "ScheduleEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduleEntryStudent_Students_StudentsId",
                        column: x => x.StudentsId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEntryStudent_StudentsId",
                table: "ScheduleEntryStudent",
                column: "StudentsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduleEntryStudent");

            migrationBuilder.AddColumn<int>(
                name: "ScheduleEntryId",
                table: "Students",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_ScheduleEntryId",
                table: "Students",
                column: "ScheduleEntryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Students_ScheduleEntries_ScheduleEntryId",
                table: "Students",
                column: "ScheduleEntryId",
                principalTable: "ScheduleEntries",
                principalColumn: "Id");
        }
    }
}
