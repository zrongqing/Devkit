using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Devkit.Business.Infrastructure.Persistence.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class InitialBusinessContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "module_buffered_command",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    source_module = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    target_module = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    sequence = table.Column<long>(type: "bigint", nullable: false),
                    command_type = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    schema_version = table.Column<int>(type: "int", nullable: false),
                    payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    idempotency_key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    correlation_id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    status = table.Column<int>(type: "int", nullable: false),
                    delivery_attempts = table.Column<int>(type: "int", nullable: false),
                    available_at_utc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    lease_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    leased_by_instance_id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    lease_expires_at_utc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: true),
                    completed_at_utc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: true),
                    last_error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_module_buffered_command", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "module_command_sequence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    target_module = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    next_value = table.Column<long>(type: "bigint", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_module_command_sequence", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "module_instance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    module_key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    instance_id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    base_address = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    state = table.Column<int>(type: "int", nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    last_heartbeat_at_utc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    stopped_at_utc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: true),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_module_instance", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "study_knowledge_base",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    tags = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("pk_study_knowledge_base", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "study_mistake",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    project_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    question_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    wrong_count = table.Column<int>(type: "int", nullable: false),
                    answer_count = table.Column<int>(type: "int", nullable: false),
                    mastered = table.Column<bool>(type: "bit", nullable: false),
                    last_answered_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("pk_study_mistake", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "study_project",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    target_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    knowledge_base_ids_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("pk_study_project", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "study_query_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    project_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    query = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    answer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    citations_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("pk_study_query_history", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workspace_file_release_outbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    entity_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    keep_if_version_exists = table.Column<bool>(type: "bit", nullable: false),
                    not_before_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workspace_file_release_outbox", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workspace_job",
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
                    table.PrimaryKey("pk_workspace_job", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "study_question",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    generation_job_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    generation_ordinal = table.Column<int>(type: "int", nullable: false),
                    knowledge_base_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    stem = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    options_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    answers_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    explanation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    citations_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    tags = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    difficulty = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("pk_study_question", x => x.id);
                    table.ForeignKey(
                        name: "fk_study_question_study_knowledge_base_knowledge_base_id",
                        column: x => x.knowledge_base_id,
                        principalTable: "study_knowledge_base",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "study_source",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    knowledge_base_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    file_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    kind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    warning = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    published_revision = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("pk_study_source", x => x.id);
                    table.ForeignKey(
                        name: "fk_study_source_study_knowledge_base_knowledge_base_id",
                        column: x => x.knowledge_base_id,
                        principalTable: "study_knowledge_base",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "study_attempt",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    project_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    mode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    deadline_utc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    submitted_at_utc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    questions_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    answers_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    score = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    pass_score = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("pk_study_attempt", x => x.id);
                    table.ForeignKey(
                        name: "fk_study_attempt_study_project_project_id",
                        column: x => x.project_id,
                        principalTable: "study_project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "study_source_version",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    source_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    file_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    blocks_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("pk_study_source_version", x => x.id);
                    table.ForeignKey(
                        name: "fk_study_source_version_study_source_source_id",
                        column: x => x.source_id,
                        principalTable: "study_source",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "study_chunk",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    knowledge_base_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    source_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    version_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ordinal = table.Column<int>(type: "int", nullable: false),
                    page = table.Column<int>(type: "int", nullable: true),
                    location = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    heading = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    embedding_profile = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("pk_study_chunk", x => x.id);
                    table.ForeignKey(
                        name: "fk_study_chunk_study_source_version_version_id",
                        column: x => x.version_id,
                        principalTable: "study_source_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_module_buffered_command_source_module_target_module_idempotency_key",
                table: "module_buffered_command",
                columns: new[] { "source_module", "target_module", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_module_buffered_command_target_module_sequence",
                table: "module_buffered_command",
                columns: new[] { "target_module", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_module_buffered_command_target_module_status_sequence",
                table: "module_buffered_command",
                columns: new[] { "target_module", "status", "sequence" });

            migrationBuilder.CreateIndex(
                name: "ix_module_command_sequence_target_module",
                table: "module_command_sequence",
                column: "target_module",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_module_instance_module_key_instance_id",
                table: "module_instance",
                columns: new[] { "module_key", "instance_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_module_instance_module_key_last_heartbeat_at_utc",
                table: "module_instance",
                columns: new[] { "module_key", "last_heartbeat_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_study_attempt_owner_id",
                table: "study_attempt",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_study_attempt_project_id",
                table: "study_attempt",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_study_attempt_status_deadline_utc",
                table: "study_attempt",
                columns: new[] { "status", "deadline_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_study_chunk_knowledge_base_id_source_id_revision",
                table: "study_chunk",
                columns: new[] { "knowledge_base_id", "source_id", "revision" });

            migrationBuilder.CreateIndex(
                name: "ix_study_chunk_owner_id",
                table: "study_chunk",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_study_chunk_version_id",
                table: "study_chunk",
                column: "version_id");

            migrationBuilder.CreateIndex(
                name: "ix_study_knowledge_base_owner_id",
                table: "study_knowledge_base",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_study_mistake_owner_id",
                table: "study_mistake",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_study_mistake_owner_id_project_id_question_id",
                table: "study_mistake",
                columns: new[] { "owner_id", "project_id", "question_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_study_project_owner_id",
                table: "study_project",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_study_query_history_owner_id",
                table: "study_query_history",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_study_query_history_project_id",
                table: "study_query_history",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_study_question_knowledge_base_id_status",
                table: "study_question",
                columns: new[] { "knowledge_base_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_study_question_owner_id",
                table: "study_question",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_study_source_knowledge_base_id",
                table: "study_source",
                column: "knowledge_base_id");

            migrationBuilder.CreateIndex(
                name: "ix_study_source_owner_id",
                table: "study_source",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_study_source_version_owner_id",
                table: "study_source_version",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_study_source_version_source_id_revision",
                table: "study_source_version",
                columns: new[] { "source_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workspace_file_release_outbox_entity_id_keep_if_version_exists",
                table: "workspace_file_release_outbox",
                columns: new[] { "entity_id", "keep_if_version_exists" });

            migrationBuilder.CreateIndex(
                name: "ix_workspace_file_release_outbox_not_before_utc",
                table: "workspace_file_release_outbox",
                column: "not_before_utc");

            migrationBuilder.CreateIndex(
                name: "ix_workspace_job_owner_id",
                table: "workspace_job",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_workspace_job_status_lease_until_utc",
                table: "workspace_job",
                columns: new[] { "status", "lease_until_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "module_buffered_command");

            migrationBuilder.DropTable(
                name: "module_command_sequence");

            migrationBuilder.DropTable(
                name: "module_instance");

            migrationBuilder.DropTable(
                name: "study_attempt");

            migrationBuilder.DropTable(
                name: "study_chunk");

            migrationBuilder.DropTable(
                name: "study_mistake");

            migrationBuilder.DropTable(
                name: "study_query_history");

            migrationBuilder.DropTable(
                name: "study_question");

            migrationBuilder.DropTable(
                name: "workspace_file_release_outbox");

            migrationBuilder.DropTable(
                name: "workspace_job");

            migrationBuilder.DropTable(
                name: "study_project");

            migrationBuilder.DropTable(
                name: "study_source_version");

            migrationBuilder.DropTable(
                name: "study_source");

            migrationBuilder.DropTable(
                name: "study_knowledge_base");
        }
    }
}
