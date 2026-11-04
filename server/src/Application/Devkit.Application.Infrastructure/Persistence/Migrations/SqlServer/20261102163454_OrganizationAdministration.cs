using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Devkit.Application.Infrastructure.Persistence.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class OrganizationAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sys_administration_event",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    occurred_utc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    actor_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    operation = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    trace_id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sys_administration_event", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sys_department",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    parent_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sys_department", x => x.id);
                    table.ForeignKey(
                        name: "fk_sys_department_sys_department_parent_id",
                        column: x => x.parent_id,
                        principalTable: "sys_department",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sys_position",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    department_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sys_position", x => x.id);
                    table.ForeignKey(
                        name: "fk_sys_position_sys_department_department_id",
                        column: x => x.department_id,
                        principalTable: "sys_department",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sys_occupancy",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    position_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sys_occupancy", x => new { x.position_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_sys_occupancy_sys_position_position_id",
                        column: x => x.position_id,
                        principalTable: "sys_position",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sys_occupancy_sys_user_user_id",
                        column: x => x.user_id,
                        principalTable: "sys_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sys_department_parent_id_name",
                table: "sys_department",
                columns: new[] { "parent_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sys_occupancy_user_id",
                table: "sys_occupancy",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_sys_position_department_id_name",
                table: "sys_position",
                columns: new[] { "department_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sys_administration_event");

            migrationBuilder.DropTable(
                name: "sys_occupancy");

            migrationBuilder.DropTable(
                name: "sys_position");

            migrationBuilder.DropTable(
                name: "sys_department");
        }
    }
}
