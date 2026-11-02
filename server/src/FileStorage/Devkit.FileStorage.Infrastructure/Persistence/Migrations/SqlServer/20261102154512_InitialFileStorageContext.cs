using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Devkit.FileStorage.Infrastructure.Persistence.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class InitialFileStorageContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "file_migration_item",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    job_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    file_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    source_location_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    target_location_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    error = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_file_migration_item", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "file_storage_job",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    kind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    target_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    payload_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    progress = table.Column<int>(type: "int", nullable: false),
                    attempts = table.Column<int>(type: "int", nullable: false),
                    error = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    lease_until_utc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    lease_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    retry_after_utc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                    owner_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    revision = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_file_storage_job", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "file_storage_location",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    root_path = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    writable = table.Column<bool>(type: "bit", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_file_storage_location", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "file_stored_file",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    content_type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    purpose = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    object_key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    length = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    location_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                    owner_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    revision = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_file_stored_file", x => x.id);
                    table.ForeignKey(
                        name: "fk_file_stored_file_file_storage_location_location_id",
                        column: x => x.location_id,
                        principalTable: "file_storage_location",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "file_reference",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    owner_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    file_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    module = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    entity_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_file_reference", x => x.id);
                    table.ForeignKey(
                        name: "fk_file_reference_file_stored_file_file_id",
                        column: x => x.file_id,
                        principalTable: "file_stored_file",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_file_migration_item_job_id_file_id",
                table: "file_migration_item",
                columns: new[] { "job_id", "file_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_file_reference_file_id_entity_id_module",
                table: "file_reference",
                columns: new[] { "file_id", "entity_id", "module" },
                unique: true,
                filter: "[is_deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "ix_file_storage_job_owner_id",
                table: "file_storage_job",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_file_storage_job_status_lease_until_utc",
                table: "file_storage_job",
                columns: new[] { "status", "lease_until_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_file_storage_location_writable",
                table: "file_storage_location",
                column: "writable",
                unique: true,
                filter: "[writable] = 1 AND [is_deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "ix_file_stored_file_location_id_status",
                table: "file_stored_file",
                columns: new[] { "location_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_file_stored_file_owner_id",
                table: "file_stored_file",
                column: "owner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "file_migration_item");

            migrationBuilder.DropTable(
                name: "file_reference");

            migrationBuilder.DropTable(
                name: "file_storage_job");

            migrationBuilder.DropTable(
                name: "file_stored_file");

            migrationBuilder.DropTable(
                name: "file_storage_location");
        }
    }
}
