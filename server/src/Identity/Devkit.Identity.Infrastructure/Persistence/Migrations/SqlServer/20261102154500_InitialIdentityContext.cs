using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Devkit.Identity.Infrastructure.Persistence.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class InitialIdentityContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sys_menu",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    menu_code = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    parent_code = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    route_key = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    icon_key = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    order = table.Column<int>(type: "int", nullable: false),
                    enabled = table.Column<bool>(type: "bit", nullable: false),
                    is_public = table.Column<bool>(type: "bit", nullable: false),
                    is_closable = table.Column<bool>(type: "bit", nullable: false),
                    source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    required_permissions_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    declaration_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    revision = table.Column<int>(type: "int", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sys_menu", x => x.id);
                    table.UniqueConstraint("ak_sys_menu_menu_code", x => x.menu_code);
                    table.ForeignKey(
                        name: "fk_sys_menu_sys_menu_parent_code",
                        column: x => x.parent_code,
                        principalTable: "sys_menu",
                        principalColumn: "menu_code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sys_menu_state",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false),
                    version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sys_menu_state", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sys_role",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    normalized_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sys_role", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sys_user",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    normalized_user_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    normalized_email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    password_hash = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    failed_access_count = table.Column<int>(type: "int", nullable: false),
                    lockout_end_utc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    authentication_version = table.Column<int>(type: "int", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sys_user", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sys_role_claim",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    role_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    claim_type = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    claim_value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sys_role_claim", x => x.id);
                    table.ForeignKey(
                        name: "fk_sys_role_claim_sys_role_role_id",
                        column: x => x.role_id,
                        principalTable: "sys_role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sys_refresh_token",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    token_hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    revoked_at_utc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: true),
                    replaced_by_token_hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sys_refresh_token", x => x.id);
                    table.ForeignKey(
                        name: "fk_sys_refresh_token_sys_user_user_id",
                        column: x => x.user_id,
                        principalTable: "sys_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sys_user_claim",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    claim_type = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    claim_value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sys_user_claim", x => x.id);
                    table.ForeignKey(
                        name: "fk_sys_user_claim_sys_user_user_id",
                        column: x => x.user_id,
                        principalTable: "sys_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sys_user_login",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    login_provider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    provider_key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    provider_display_name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sys_user_login", x => x.id);
                    table.ForeignKey(
                        name: "fk_sys_user_login_sys_user_user_id",
                        column: x => x.user_id,
                        principalTable: "sys_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sys_user_role",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    role_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sys_user_role", x => x.id);
                    table.ForeignKey(
                        name: "fk_sys_user_role_sys_role_role_id",
                        column: x => x.role_id,
                        principalTable: "sys_role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sys_user_role_sys_user_user_id",
                        column: x => x.user_id,
                        principalTable: "sys_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sys_user_token",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    login_provider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sys_user_token", x => x.id);
                    table.ForeignKey(
                        name: "fk_sys_user_token_sys_user_user_id",
                        column: x => x.user_id,
                        principalTable: "sys_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "sys_menu_state",
                columns: new[] { "id", "version" },
                values: new object[] { 1, 0 });

            migrationBuilder.CreateIndex(
                name: "ix_sys_menu_parent_code",
                table: "sys_menu",
                column: "parent_code");

            migrationBuilder.CreateIndex(
                name: "ix_sys_refresh_token_token_hash",
                table: "sys_refresh_token",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sys_refresh_token_user_id_expires_at_utc",
                table: "sys_refresh_token",
                columns: new[] { "user_id", "expires_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_sys_role_normalized_name",
                table: "sys_role",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sys_role_claim_role_id",
                table: "sys_role_claim",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_sys_user_normalized_email",
                table: "sys_user",
                column: "normalized_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sys_user_normalized_user_name",
                table: "sys_user",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sys_user_claim_user_id",
                table: "sys_user_claim",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_sys_user_login_login_provider_provider_key",
                table: "sys_user_login",
                columns: new[] { "login_provider", "provider_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sys_user_login_user_id",
                table: "sys_user_login",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_sys_user_role_role_id",
                table: "sys_user_role",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_sys_user_role_user_id_role_id",
                table: "sys_user_role",
                columns: new[] { "user_id", "role_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sys_user_token_user_id_login_provider_name",
                table: "sys_user_token",
                columns: new[] { "user_id", "login_provider", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sys_menu");

            migrationBuilder.DropTable(
                name: "sys_menu_state");

            migrationBuilder.DropTable(
                name: "sys_refresh_token");

            migrationBuilder.DropTable(
                name: "sys_role_claim");

            migrationBuilder.DropTable(
                name: "sys_user_claim");

            migrationBuilder.DropTable(
                name: "sys_user_login");

            migrationBuilder.DropTable(
                name: "sys_user_role");

            migrationBuilder.DropTable(
                name: "sys_user_token");

            migrationBuilder.DropTable(
                name: "sys_role");

            migrationBuilder.DropTable(
                name: "sys_user");
        }
    }
}
