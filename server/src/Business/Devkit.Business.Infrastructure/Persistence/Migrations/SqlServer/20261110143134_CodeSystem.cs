using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Devkit.Business.Infrastructure.Persistence.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class CodeSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sys_code_counter",
                columns: table => new
                {
                    rule_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    period_key = table.Column<string>(type: "varchar(8)", unicode: false, maxLength: 8, nullable: false, collation: "Latin1_General_100_BIN2"),
                    last_value = table.Column<long>(type: "bigint", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sys_code_counter", x => new { x.rule_id, x.period_key });
                    table.CheckConstraint("ck_sys_code_counter_value", "last_value BETWEEN 1 AND 999999999999");
                });

            migrationBuilder.CreateTable(
                name: "sys_code_event",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    rule_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    action = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    version_no = table.Column<int>(type: "int", nullable: true),
                    actor_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    reason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sys_code_event", x => x.id);
                    table.CheckConstraint("ck_sys_code_event_action", "action IN ('create','save_draft','publish','disable','enable')");
                });

            migrationBuilder.CreateTable(
                name: "sys_code_issue",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    rule_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    version_no = table.Column<int>(type: "int", nullable: false),
                    period_key = table.Column<string>(type: "varchar(8)", unicode: false, maxLength: 8, nullable: false, collation: "Latin1_General_100_BIN2"),
                    sequence_value = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    request_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    business_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    issued_by = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    issued_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sys_code_issue", x => x.id);
                    table.CheckConstraint("ck_sys_code_issue_value", "sequence_value BETWEEN 1 AND 999999999999");
                    table.ForeignKey(
                        name: "fk_sys_code_issue_sys_code_counter_rule_id_period_key",
                        columns: x => new { x.rule_id, x.period_key },
                        principalTable: "sys_code_counter",
                        principalColumns: new[] { "rule_id", "period_key" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sys_code_rule",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    rule_key = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    module_key = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false, collation: "Latin1_General_100_BIN2"),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    active_version_no = table.Column<int>(type: "int", nullable: true),
                    draft_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sys_code_rule", x => x.id);
                    table.CheckConstraint("ck_sys_code_rule_draft", "draft_json IS NULL OR ISJSON(draft_json) = 1");
                    table.CheckConstraint("ck_sys_code_rule_revision", "revision >= 1");
                    table.CheckConstraint("ck_sys_code_rule_status", "(status = 'unpublished' AND active_version_no IS NULL) OR (status IN ('enabled','disabled') AND active_version_no IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "sys_code_version",
                columns: table => new
                {
                    rule_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    version_no = table.Column<int>(type: "int", nullable: false),
                    definition_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    reason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    published_by = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    published_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sys_code_version", x => new { x.rule_id, x.version_no });
                    table.CheckConstraint("ck_sys_code_version_definition", "ISJSON(definition_json) = 1");
                    table.CheckConstraint("ck_sys_code_version_number", "version_no >= 1");
                    table.ForeignKey(
                        name: "fk_sys_code_version_sys_code_rule_rule_id",
                        column: x => x.rule_id,
                        principalTable: "sys_code_rule",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sys_code_event_rule_id_occurred_at_utc_id",
                table: "sys_code_event",
                columns: new[] { "rule_id", "occurred_at_utc", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_sys_code_event_rule_id_revision",
                table: "sys_code_event",
                columns: new[] { "rule_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sys_code_event_rule_id_version_no",
                table: "sys_code_event",
                columns: new[] { "rule_id", "version_no" });

            migrationBuilder.CreateIndex(
                name: "ix_sys_code_issue_rule_id_business_id",
                table: "sys_code_issue",
                columns: new[] { "rule_id", "business_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sys_code_issue_rule_id_code",
                table: "sys_code_issue",
                columns: new[] { "rule_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sys_code_issue_rule_id_issued_at_utc_id",
                table: "sys_code_issue",
                columns: new[] { "rule_id", "issued_at_utc", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_sys_code_issue_rule_id_period_key_sequence_value",
                table: "sys_code_issue",
                columns: new[] { "rule_id", "period_key", "sequence_value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sys_code_issue_rule_id_request_id",
                table: "sys_code_issue",
                columns: new[] { "rule_id", "request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sys_code_issue_rule_id_version_no",
                table: "sys_code_issue",
                columns: new[] { "rule_id", "version_no" });

            migrationBuilder.CreateIndex(
                name: "ix_sys_code_rule_id_active_version_no",
                table: "sys_code_rule",
                columns: new[] { "id", "active_version_no" });

            migrationBuilder.CreateIndex(
                name: "ix_sys_code_rule_module_key_status_updated_at_utc_id",
                table: "sys_code_rule",
                columns: new[] { "module_key", "status", "updated_at_utc", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_sys_code_rule_rule_key",
                table: "sys_code_rule",
                column: "rule_key",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_sys_code_counter_sys_code_rule_rule_id",
                table: "sys_code_counter",
                column: "rule_id",
                principalTable: "sys_code_rule",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sys_code_event_sys_code_rule_rule_id",
                table: "sys_code_event",
                column: "rule_id",
                principalTable: "sys_code_rule",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sys_code_event_sys_code_version_rule_id_version_no",
                table: "sys_code_event",
                columns: new[] { "rule_id", "version_no" },
                principalTable: "sys_code_version",
                principalColumns: new[] { "rule_id", "version_no" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sys_code_issue_sys_code_version_rule_id_version_no",
                table: "sys_code_issue",
                columns: new[] { "rule_id", "version_no" },
                principalTable: "sys_code_version",
                principalColumns: new[] { "rule_id", "version_no" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sys_code_rule_sys_code_version_id_active_version_no",
                table: "sys_code_rule",
                columns: new[] { "id", "active_version_no" },
                principalTable: "sys_code_version",
                principalColumns: new[] { "rule_id", "version_no" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_sys_code_version_sys_code_rule_rule_id",
                table: "sys_code_version");

            migrationBuilder.DropTable(
                name: "sys_code_event");

            migrationBuilder.DropTable(
                name: "sys_code_issue");

            migrationBuilder.DropTable(
                name: "sys_code_counter");

            migrationBuilder.DropTable(
                name: "sys_code_rule");

            migrationBuilder.DropTable(
                name: "sys_code_version");
        }
    }
}
