using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Devkit.Application.Infrastructure.Persistence.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class UserRoleView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CREATE VIEW must start its batch, including in EF's idempotent scripts.
            migrationBuilder.Sql("""
                EXEC(N'CREATE VIEW [dbo].[vw_sys_user_role]
                AS
                SELECT
                    ur.[id],
                    ur.[user_id],
                    u.[user_name],
                    ur.[role_id],
                    r.[name] AS [role_name],
                    ur.[created_by],
                    ur.[updated_by],
                    ur.[created_at_utc],
                    ur.[updated_at_utc],
                    DATEADD(HOUR, 8, ur.[created_at_utc]) AS [created_at_beijing],
                    DATEADD(HOUR, 8, ur.[updated_at_utc]) AS [updated_at_beijing]
                FROM [dbo].[sys_user_role] AS ur
                INNER JOIN [dbo].[sys_user] AS u ON u.[id] = ur.[user_id]
                INNER JOIN [dbo].[sys_role] AS r ON r.[id] = ur.[role_id]
                WHERE ur.[is_deleted] = 0
                  AND u.[is_deleted] = 0
                  AND r.[is_deleted] = 0;');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW [dbo].[vw_sys_user_role];");
        }
    }
}
