using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Studue;
using WebPush;

#nullable disable

namespace StudueSharp.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Configs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Data = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configs", x => x.Id);
                });

            var vapidKeys = VapidHelper.GenerateVapidKeys();
            migrationBuilder.InsertData(table: "Configs", columns: ["Id", "Data"], values: ["VapidKey.Public", vapidKeys.PublicKey]);
            migrationBuilder.InsertData(table: "Configs", columns: ["Id", "Data"], values: ["VapidKey.Private", vapidKeys.PrivateKey]);
            migrationBuilder.InsertData(table: "Configs", columns: ["Id", "Data"], values: ["LastNotificationTime", Helper.Now().ToString(CultureInfo.InvariantCulture)]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(table: "Configs", keyColumn: "Id", keyValue: "VapidKey.Public");
            migrationBuilder.DeleteData(table: "Configs", keyColumn: "Id", keyValue: "VapidKey.Private");
            migrationBuilder.DeleteData(table: "Configs", keyColumn: "Id", keyValue: "LastNotificationTime");

            migrationBuilder.DropTable(
                name: "Configs");
        }
    }
}
