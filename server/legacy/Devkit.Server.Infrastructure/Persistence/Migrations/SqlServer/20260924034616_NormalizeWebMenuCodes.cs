using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Devkit.Server.Infrastructure.Persistence.Migrations.SqlServer;

public partial class NormalizeWebMenuCodes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            -- Lock the menu state for the entire EF migration transaction.
            DECLARE @version int;
            SELECT @version = Version FROM dbo.WebMenuState WITH (UPDLOCK, HOLDLOCK) WHERE Id = 1;
            CREATE TABLE #MenuCodeMap (OldCode nvarchar(120) NOT NULL PRIMARY KEY, NewCode nvarchar(120) NOT NULL);
            INSERT INTO #MenuCodeMap (OldCode, NewCode)
            SELECT Code, CASE Code
                WHEN N'study-project-management' THEN N'knowledge.projects'
                WHEN N'study-knowledge' THEN N'knowledge.bases'
                WHEN N'study-projects' THEN N'knowledge.search'
                WHEN N'study-practice' THEN N'knowledge.practice.exam'
                WHEN N'study-questions' THEN N'knowledge.practice.questions'
                WHEN N'study-progress' THEN N'knowledge.practice.progress'
                WHEN N'study-jobs' THEN N'system.monitor.jobs'
                WHEN N'system-users' THEN N'system.identity.users'
                WHEN N'system-roles' THEN N'system.identity.roles'
                WHEN N'system-identity' THEN N'system.identity.permissions'
                WHEN N'system-menus' THEN N'system.menus'
                WHEN N'system-files' THEN N'system.files'
                WHEN N'system-storage' THEN N'system.storage'
                WHEN N'system-status' THEN N'system.monitor.status'
                WHEN N'settings' THEN N'system.settings'
                WHEN N'about' THEN N'system.about'
                WHEN N'system-management' THEN N'system.management'
                WHEN N'system-management.users' THEN N'system.identity'
                WHEN N'system' THEN N'system.monitor'
                ELSE REPLACE(REPLACE(Code, N'-', N'.'), N'_', N'.') END
            FROM (
                SELECT MenuCode AS Code FROM dbo.WebMenus
                UNION SELECT ParentCode FROM dbo.WebMenus WHERE ParentCode IS NOT NULL
                UNION SELECT JSON_VALUE(DeclarationJson, '$.menuCode') FROM dbo.WebMenus WHERE DeclarationJson IS NOT NULL
                UNION SELECT JSON_VALUE(DeclarationJson, '$.parentCode') FROM dbo.WebMenus WHERE DeclarationJson IS NOT NULL
                UNION SELECT ClaimValue FROM dbo.UserClaims WHERE ClaimType = N'menu:web'
                UNION SELECT ClaimValue FROM dbo.RoleClaims WHERE ClaimType = N'menu:web'
            ) AS Codes WHERE Code IS NOT NULL;
            
            -- Do not merge distinct identities or silently discard grants, including archived records.
            IF EXISTS (SELECT map.NewCode FROM dbo.WebMenus m JOIN #MenuCodeMap map ON map.OldCode=m.MenuCode GROUP BY map.NewCode HAVING COUNT(*) > 1)
                THROW 51000, 'Menu code normalization has a collision. Resolve duplicate target codes before retrying.', 1;
            IF EXISTS (
                SELECT 1 FROM #MenuCodeMap map
                WHERE map.NewCode COLLATE Latin1_General_100_BIN2 LIKE N'%[^a-z0-9.]%'
                    OR LEFT(map.NewCode,1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[a-z]'
                    OR RIGHT(map.NewCode,1)=N'.' OR map.NewCode LIKE N'%..%'
                    OR map.NewCode COLLATE Latin1_General_100_BIN2 LIKE N'%.[0-9]%'
            )
                THROW 51001, 'Menu code normalization has an invalid business segment. Resolve legacy custom codes before retrying.', 1;
            
            ALTER TABLE dbo.WebMenus DROP CONSTRAINT FK_WebMenus_WebMenus_ParentCode;
            UPDATE m SET MenuCode=code.NewCode, ParentCode=parent.NewCode,
                Revision=Revision+1, UpdatedAtUtc=SYSUTCDATETIME()
            FROM dbo.WebMenus m JOIN #MenuCodeMap code ON code.OldCode=m.MenuCode
            LEFT JOIN #MenuCodeMap parent ON parent.OldCode=m.ParentCode;
            UPDATE m SET DeclarationJson=JSON_MODIFY(m.DeclarationJson, '$.menuCode', code.NewCode)
            FROM dbo.WebMenus m JOIN #MenuCodeMap code ON code.OldCode=JSON_VALUE(m.DeclarationJson, '$.menuCode');
            UPDATE m SET DeclarationJson=JSON_MODIFY(m.DeclarationJson, '$.parentCode', code.NewCode)
            FROM dbo.WebMenus m JOIN #MenuCodeMap code ON code.OldCode=JSON_VALUE(m.DeclarationJson, '$.parentCode');
            UPDATE claim SET ClaimValue=code.NewCode
            FROM dbo.UserClaims claim JOIN #MenuCodeMap code ON code.OldCode=claim.ClaimValue WHERE claim.ClaimType=N'menu:web';
            UPDATE claim SET ClaimValue=code.NewCode
            FROM dbo.RoleClaims claim JOIN #MenuCodeMap code ON code.OldCode=claim.ClaimValue WHERE claim.ClaimType=N'menu:web';
            ALTER TABLE dbo.WebMenus WITH CHECK ADD CONSTRAINT FK_WebMenus_WebMenus_ParentCode
                FOREIGN KEY (ParentCode) REFERENCES dbo.WebMenus(MenuCode);
            UPDATE dbo.WebMenuState SET Version=Version+1 WHERE Id=1 AND Version>0;
            DROP TABLE #MenuCodeMap;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Custom '-' and '_' separators cannot be reconstructed from the normalized code.
        throw new NotSupportedException("Restore the pre-migration backup to recover original menu codes and grants.");
    }
}
