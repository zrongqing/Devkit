using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Devkit.Application.Infrastructure.Persistence.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class SystemSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sys_system_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false),
                    time_zone = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sys_system_settings", x => x.id);
                    table.CheckConstraint("ck_sys_system_settings_singleton", "id = 1");
                });

            migrationBuilder.InsertData(
                table: "sys_system_settings",
                columns: new[] { "id", "time_zone" },
                values: new object[] { 1, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sys_system_settings");
        }
    }
}
