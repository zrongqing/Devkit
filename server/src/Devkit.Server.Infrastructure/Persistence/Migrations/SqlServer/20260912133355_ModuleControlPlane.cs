using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Devkit.Server.Infrastructure.Persistence.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class ModuleControlPlane : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BufferedModuleCommands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceModule = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetModule = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    CommandType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DeliveryAttempts = table.Column<int>(type: "int", nullable: false),
                    AvailableAtUtc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    LeaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeasedByInstanceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BufferedModuleCommands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModuleCommandSequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetModule = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NextValue = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleCommandSequences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModuleInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModuleKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InstanceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BaseAddress = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    LastHeartbeatAtUtc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    StoppedAtUtc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleInstances", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BufferedModuleCommands_SourceModule_TargetModule_IdempotencyKey",
                table: "BufferedModuleCommands",
                columns: new[] { "SourceModule", "TargetModule", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BufferedModuleCommands_TargetModule_Sequence",
                table: "BufferedModuleCommands",
                columns: new[] { "TargetModule", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BufferedModuleCommands_TargetModule_Status_Sequence",
                table: "BufferedModuleCommands",
                columns: new[] { "TargetModule", "Status", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_ModuleCommandSequences_TargetModule",
                table: "ModuleCommandSequences",
                column: "TargetModule",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModuleInstances_ModuleKey_InstanceId",
                table: "ModuleInstances",
                columns: new[] { "ModuleKey", "InstanceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModuleInstances_ModuleKey_LastHeartbeatAtUtc",
                table: "ModuleInstances",
                columns: new[] { "ModuleKey", "LastHeartbeatAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BufferedModuleCommands");

            migrationBuilder.DropTable(
                name: "ModuleCommandSequences");

            migrationBuilder.DropTable(
                name: "ModuleInstances");
        }
    }
}
