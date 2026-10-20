using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Devkit.Business.Infrastructure.Persistence.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class ExtractFileStorageContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Retain legacy file tables for the explicit, offline data transfer and rollback.
            migrationBuilder.CreateTable(
                name: "FileReleaseOutbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KeepIfVersionExists = table.Column<bool>(type: "bit", nullable: false),
                    NotBeforeUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileReleaseOutbox", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileReleaseOutbox_EntityId_KeepIfVersionExists",
                table: "FileReleaseOutbox",
                columns: new[] { "EntityId", "KeepIfVersionExists" });

            migrationBuilder.CreateIndex(
                name: "IX_FileReleaseOutbox_NotBeforeUtc",
                table: "FileReleaseOutbox",
                column: "NotBeforeUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileReleaseOutbox");

        }
    }
}
