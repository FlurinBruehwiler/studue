using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudueSharp.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Modules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Modules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StudentId = table.Column<string>(type: "TEXT", nullable: false),
                    Class = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModuleInstances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ModuleId = table.Column<int>(type: "INTEGER", nullable: false),
                    LessionsId = table.Column<string>(type: "TEXT", nullable: false),
                    ProfessorNames = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModuleInstances_Modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Assignements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ModuleInstanceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    DueDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Mandatory = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedById = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedById = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assignements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Assignements_ModuleInstances_ModuleInstanceId",
                        column: x => x.ModuleInstanceId,
                        principalTable: "ModuleInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Assignements_Students_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Assignements_Students_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModuleInstanceStudent",
                columns: table => new
                {
                    ModuleInstancesId = table.Column<int>(type: "INTEGER", nullable: false),
                    StudentsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleInstanceStudent", x => new { x.ModuleInstancesId, x.StudentsId });
                    table.ForeignKey(
                        name: "FK_ModuleInstanceStudent_ModuleInstances_ModuleInstancesId",
                        column: x => x.ModuleInstancesId,
                        principalTable: "ModuleInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModuleInstanceStudent_Students_StudentsId",
                        column: x => x.StudentsId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Assignements_CreatedById",
                table: "Assignements",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Assignements_ModuleInstanceId",
                table: "Assignements",
                column: "ModuleInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_Assignements_UpdatedById",
                table: "Assignements",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ModuleInstances_ModuleId",
                table: "ModuleInstances",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ModuleInstanceStudent_StudentsId",
                table: "ModuleInstanceStudent",
                column: "StudentsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Assignements");

            migrationBuilder.DropTable(
                name: "ModuleInstanceStudent");

            migrationBuilder.DropTable(
                name: "ModuleInstances");

            migrationBuilder.DropTable(
                name: "Students");

            migrationBuilder.DropTable(
                name: "Modules");
        }
    }
}
