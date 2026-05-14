using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudueSharp.Migrations
{
    /// <inheritdoc />
    public partial class BigExtensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LessionsId",
                table: "ModuleInstances");

            migrationBuilder.AddColumn<string>(
                name: "Semester",
                table: "ModuleInstances",
                type: "TEXT",
                nullable: false,
                defaultValue: "OLD");

            migrationBuilder.AddColumn<int>(
                name: "ScheduleEntryId",
                table: "Students",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AssignmentStudent",
                columns: table => new
                {
                    CompletedAssignmentsId = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedByStudentsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentStudent", x => new { x.CompletedAssignmentsId, x.CompletedByStudentsId });
                    table.ForeignKey(
                        name: "FK_AssignmentStudent_Assignements_CompletedAssignmentsId",
                        column: x => x.CompletedAssignmentsId,
                        principalTable: "Assignements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssignmentStudent_Students_CompletedByStudentsId",
                        column: x => x.CompletedByStudentsId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PushSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Endpoint = table.Column<string>(type: "TEXT", nullable: false),
                    P256DH = table.Column<string>(type: "TEXT", nullable: false),
                    Auth = table.Column<string>(type: "TEXT", nullable: false),
                    StudentId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PushSubscriptions_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Semester = table.Column<string>(type: "TEXT", nullable: false),
                    ZhawID = table.Column<int>(type: "INTEGER", nullable: false),
                    ModuleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Teacher = table.Column<string>(type: "TEXT", nullable: false),
                    Room = table.Column<string>(type: "TEXT", nullable: false),
                    Weekday = table.Column<int>(type: "INTEGER", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    ModuleInstanceId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleEntries_ModuleInstances_ModuleInstanceId",
                        column: x => x.ModuleInstanceId,
                        principalTable: "ModuleInstances",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ScheduleEntries_Modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Students_ScheduleEntryId",
                table: "Students",
                column: "ScheduleEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentStudent_CompletedByStudentsId",
                table: "AssignmentStudent",
                column: "CompletedByStudentsId");

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_StudentId",
                table: "PushSubscriptions",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEntries_ModuleId",
                table: "ScheduleEntries",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEntries_ModuleInstanceId",
                table: "ScheduleEntries",
                column: "ModuleInstanceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Students_ScheduleEntries_ScheduleEntryId",
                table: "Students",
                column: "ScheduleEntryId",
                principalTable: "ScheduleEntries",
                principalColumn: "Id");

            migrationBuilder.DropColumn(
                name: "ProfessorNames",
                table: "ModuleInstances");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Students_ScheduleEntries_ScheduleEntryId",
                table: "Students");

            migrationBuilder.DropTable(
                name: "AssignmentStudent");

            migrationBuilder.DropTable(
                name: "PushSubscriptions");

            migrationBuilder.DropTable(
                name: "ScheduleEntries");

            migrationBuilder.DropIndex(
                name: "IX_Students_ScheduleEntryId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "ScheduleEntryId",
                table: "Students");

            migrationBuilder.AddColumn<string>(
                name: "ProfessorNames",
                table: "ModuleInstances",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LessionsId",
                table: "ModuleInstances",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.DropColumn(
                name: "Semester",
                table: "ModuleInstances");
        }
    }
}
