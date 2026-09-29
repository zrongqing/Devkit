using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Devkit.Server.Infrastructure.Persistence.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class AddWebMenuManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WebMenus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MenuCode = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ParentCode = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    RouteKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IconKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    IsPublic = table.Column<bool>(type: "bit", nullable: false),
                    IsClosable = table.Column<bool>(type: "bit", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequiredPermissionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeclarationJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebMenus", x => x.Id);
                    table.UniqueConstraint("AK_WebMenus_MenuCode", x => x.MenuCode);
                    table.ForeignKey(
                        name: "FK_WebMenus_WebMenus_ParentCode",
                        column: x => x.ParentCode,
                        principalTable: "WebMenus",
                        principalColumn: "MenuCode",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WebMenuState",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebMenuState", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "WebMenuState",
                columns: new[] { "Id", "Version" },
                values: new object[] { 1, 0 });

            migrationBuilder.CreateIndex(
                name: "IX_WebMenus_ParentCode",
                table: "WebMenus",
                column: "ParentCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WebMenus");

            migrationBuilder.DropTable(
                name: "WebMenuState");
        }
    }
}
