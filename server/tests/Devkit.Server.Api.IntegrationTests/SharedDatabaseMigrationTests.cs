using Devkit.Business.Infrastructure.Persistence;
using Devkit.Business.Infrastructure.Persistence.Migrations.SqlServer;
using Devkit.FileStorage.Infrastructure.Persistence;
using Devkit.FileStorage.Infrastructure.Persistence.Migrations.SqlServer;
using Devkit.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace Devkit.Server.Api.IntegrationTests;

public sealed class SharedDatabaseMigrationTests
{
    [Fact]
    public void Initial_migrations_create_each_table_in_only_its_own_context()
    {
        var business = new InitialBusinessContext { ActiveProvider = "Microsoft.EntityFrameworkCore.SqlServer" };
        var files = new[] { "FileMigrationItems", "FileReferences", "StoredFiles", "StorageLocations" };
        Assert.DoesNotContain(business.UpOperations.OfType<CreateTableOperation>(), table => files.Contains(table.Name));
        Assert.DoesNotContain(business.UpOperations.OfType<CreateTableOperation>().SelectMany(table => table.ForeignKeys),
            key => files.Contains(key.PrincipalTable));
        Assert.DoesNotContain(business.DownOperations.OfType<DropTableOperation>(), table => files.Contains(table.Name));

        var initialFiles = new InitialFileStorageContext { ActiveProvider = "Microsoft.EntityFrameworkCore.SqlServer" };
        foreach (var table in files.Append("StorageJobs"))
            Assert.Contains(initialFiles.UpOperations.OfType<SqlOperation>(), operation =>
                operation.Sql.Contains($"IF OBJECT_ID(N'[dbo].[{table}]', N'U') IS NULL", StringComparison.Ordinal)
                && operation.Sql.Contains($"CREATE TABLE [{table}]", StringComparison.Ordinal));
        Assert.Contains(initialFiles.UpOperations.OfType<SqlOperation>(), operation => operation.Sql.Contains("DATALENGTH([Module]) > 900"));
    }

    [Fact]
    public void SQL_Server_migration_scripts_keep_shared_history_and_guard_existing_file_tables()
    {
        using var identity = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlServer().Options);
        using var business = new BusinessDbContext(new DbContextOptionsBuilder<BusinessDbContext>().UseSqlServer().Options);
        using var files = new FileStorageDbContext(new DbContextOptionsBuilder<FileStorageDbContext>().UseSqlServer().Options);
        foreach (var context in new DbContext[] { identity, business, files })
        {
            var script = context.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
            Assert.Contains("__EFMigrationsHistory", script);
            if (context is BusinessDbContext) Assert.DoesNotContain("CREATE TABLE [StoredFiles]", script);
            if (context is FileStorageDbContext)
            {
                Assert.Contains("IF OBJECT_ID(N'[dbo].[StoredFiles]', N'U') IS NULL", script);
                Assert.Contains("IF NOT EXISTS (SELECT 1 FROM sys.indexes", script);
            }
        }
    }
}
