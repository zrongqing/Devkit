using Devkit.Business.Infrastructure.Persistence;
using Devkit.FileStorage.Infrastructure.Persistence;
using Devkit.Application.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace Devkit.Server.Api.IntegrationTests;

public sealed class SharedDatabaseMigrationTests
{
    [Fact]
    public void User_role_view_upgrade_and_rollback_only_change_the_view()
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer().Options);
        var assembly = context.GetService<IMigrationsAssembly>();
        const string migrationId = "20261104131144_UserRoleView";
        var migration = assembly.CreateMigration(assembly.Migrations[migrationId], context.Database.ProviderName!);
        var sql = Assert.IsType<SqlOperation>(Assert.Single(migration.UpOperations)).Sql;
        Assert.Contains("EXEC(N'CREATE VIEW [dbo].[vw_sys_user_role]", sql);
        Assert.Contains("u.[user_name]", sql);
        Assert.Contains("r.[name] AS [role_name]", sql);
        Assert.Contains("u.[id] = ur.[user_id]", sql);
        Assert.Contains("r.[id] = ur.[role_id]", sql);
        foreach (var timestamp in new[] { "created", "updated" })
        {
            Assert.Contains($"ur.[{timestamp}_at_utc],", sql);
            Assert.Contains($"DATEADD(HOUR, 8, ur.[{timestamp}_at_utc]) AS [{timestamp}_at_beijing]", sql);
        }
        foreach (var alias in new[] { "ur", "u", "r" })
            Assert.Contains($"{alias}.[is_deleted] = 0", sql);

        var migrator = context.GetService<IMigrator>();
        var upgrade = migrator.GenerateScript("20261102163454_OrganizationAdministration", migrationId,
            MigrationsSqlGenerationOptions.Idempotent);
        // The idempotent generator indents each SQL line inside its history guard.
        Assert.All(sql.Split('\n'), line => Assert.Contains(line.Trim(), upgrade));
        Assert.Contains("IF NOT EXISTS", upgrade);
        Assert.DoesNotContain("CREATE TABLE [sys_", upgrade);
        Assert.DoesNotContain("DROP TABLE", upgrade);
        Assert.DoesNotContain("DELETE FROM", upgrade);
        var rollback = Assert.IsType<SqlOperation>(Assert.Single(migration.DownOperations)).Sql;
        Assert.Equal("DROP VIEW [dbo].[vw_sys_user_role];", rollback);
        Assert.Contains(rollback, migrator.GenerateScript(migrationId, "20261102163454_OrganizationAdministration"));
    }

    [Fact]
    public void Organization_upgrade_script_only_adds_organization_and_audit_tables()
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer().Options);
        var script = context.GetService<IMigrator>().GenerateScript(
            "20261102154500_InitialIdentityContext", "20261102163454_OrganizationAdministration",
            MigrationsSqlGenerationOptions.Idempotent);
        foreach (var table in new[] { "sys_administration_event", "sys_department", "sys_position", "sys_occupancy" })
            Assert.Contains($"CREATE TABLE [{table}]", script);
        Assert.DoesNotContain("CREATE TABLE [sys_user]", script);
        Assert.DoesNotContain("DROP TABLE", script);
        Assert.DoesNotContain("DELETE FROM", script);
        Assert.DoesNotContain("20261102154500_InitialIdentityContext", script);
        Assert.Contains("20261102163454_OrganizationAdministration", script);
        Assert.Contains("IF NOT EXISTS", script);
    }

    [Fact]
    public void Baselines_create_and_drop_only_their_owned_tables_with_unique_migration_ids()
    {
        using var identity = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer().Options);
        using var business = new BusinessDbContext(new DbContextOptionsBuilder<BusinessDbContext>().UseSqlServer().Options);
        using var files = new FileStorageDbContext(new DbContextOptionsBuilder<FileStorageDbContext>().UseSqlServer().Options);
        var ids = new HashSet<string>();
        var allTables = new HashSet<string>();
        foreach (var context in new DbContext[] { identity, business, files })
        {
            var assembly = context.GetService<IMigrationsAssembly>();
            var migrations = assembly.Migrations.Select(entry =>
            {
                Assert.True(ids.Add(entry.Key), $"Duplicate migration ID: {entry.Key}");
                return assembly.CreateMigration(entry.Value, context.Database.ProviderName!);
            }).ToArray();
            var modelTables = context.GetService<IDesignTimeModel>().Model.GetRelationalModel().Tables.ToArray();
            var tableMigrations = migrations.Where(m => m.UpOperations.Any(operation => operation is CreateTableOperation)).ToArray();
            var created = migrations.SelectMany(m => m.UpOperations).OfType<CreateTableOperation>().ToArray();
            var dropped = tableMigrations.SelectMany(m => m.DownOperations).Select(operation => Assert.IsType<DropTableOperation>(operation)).ToArray();
            Assert.Equal(modelTables.Select(t => t.Name).Order(), created.Select(t => t.Name).Order());
            Assert.Equal(created.Select(t => t.Name).Order(), dropped.Select(t => t.Name).Order());
            // Express the new schema directly, without historical table-adoption SQL.
            Assert.DoesNotContain(tableMigrations.SelectMany(m => m.UpOperations), operation => operation is SqlOperation);
            foreach (var table in created)
            {
                Assert.True(allTables.Add(table.Name), $"Table owned by two contexts: {table.Name}");
                var modelTable = Assert.Single(modelTables, t => t.Name == table.Name);
                Assert.Equal(modelTable.Columns.Select(c => c.Name).Order(), table.Columns.Select(c => c.Name).Order());
                foreach (var foreignKey in table.ForeignKeys)
                {
                    var principal = Assert.Single(created, t => t.Name == foreignKey.PrincipalTable);
                    Assert.All(foreignKey.Columns, column => Assert.Contains(table.Columns, c => c.Name == column));
                    Assert.NotNull(foreignKey.PrincipalColumns);
                    Assert.All(foreignKey.PrincipalColumns, column => Assert.Contains(principal.Columns, c => c.Name == column));
                }
            }
            foreach (var index in migrations.SelectMany(m => m.UpOperations).OfType<CreateIndexOperation>())
            {
                var table = Assert.Single(created, t => t.Name == index.Table);
                Assert.All(index.Columns, column => Assert.Contains(table.Columns, c => c.Name == column));
            }
        }
        Assert.Equal(33, allTables.Count);
    }

    [Fact]
    public void File_baseline_declares_module_length_and_filtered_uniqueness_without_legacy_adoption()
    {
        var migration = new Devkit.FileStorage.Infrastructure.Persistence.Migrations.SqlServer.InitialFileStorageContext();
        var table = Assert.Single(migration.UpOperations.OfType<CreateTableOperation>(), t => t.Name == "file_reference");
        var module = Assert.Single(table.Columns, c => c.Name == "module");
        Assert.Equal("nvarchar(450)", module.ColumnType);
        Assert.False(module.IsNullable);
        var references = Assert.Single(migration.UpOperations.OfType<CreateIndexOperation>(),
            i => i.Table == "file_reference" && i.Columns.SequenceEqual(new[] { "file_id", "entity_id", "module" }));
        Assert.True(references.IsUnique);
        Assert.Equal("[is_deleted] = 0", references.Filter);
        var writable = Assert.Single(migration.UpOperations.OfType<CreateIndexOperation>(),
            i => i.Table == "file_storage_location" && i.Columns.SequenceEqual(new[] { "writable" }));
        Assert.True(writable.IsUnique);
        Assert.Equal("[writable] = 1 AND [is_deleted] = 0", writable.Filter);
    }
}
