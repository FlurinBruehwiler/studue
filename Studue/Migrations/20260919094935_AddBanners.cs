using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudueSharp.Migrations
{
    /// <inheritdoc />
    public partial class AddBanners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Banners",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    LinkUrl = table.Column<string>(type: "TEXT", nullable: true),
                    LinkText = table.Column<string>(type: "TEXT", nullable: true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndsAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Banners", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "BannerStudent",
                columns: table => new
                {
                    DismissedBannersId = table.Column<int>(type: "INTEGER", nullable: false),
                    DismissedByStudentsId = table.Column<int>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_BannerStudent",
                        x => new { x.DismissedBannersId, x.DismissedByStudentsId }
                    );
                    table.ForeignKey(
                        name: "FK_BannerStudent_Banners_DismissedBannersId",
                        column: x => x.DismissedBannersId,
                        principalTable: "Banners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_BannerStudent_Students_DismissedByStudentsId",
                        column: x => x.DismissedByStudentsId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_BannerStudent_DismissedByStudentsId",
                table: "BannerStudent",
                column: "DismissedByStudentsId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "BannerStudent");

            migrationBuilder.DropTable(name: "Banners");
        }
    }
}
