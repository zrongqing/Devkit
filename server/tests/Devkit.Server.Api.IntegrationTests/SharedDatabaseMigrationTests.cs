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
            var created = migrations.SelectMany(m => m.UpOperations).OfType<CreateTableOperation>().ToArray();
            var dropped = migrations.SelectMany(m => m.DownOperations).Select(operation => Assert.IsType<DropTableOperation>(operation)).ToArray();
            Assert.Equal(modelTables.Select(t => t.Name).Order(), created.Select(t => t.Name).Order());
            Assert.Equal(created.Select(t => t.Name).Order(), dropped.Select(t => t.Name).Order());
            // Express the new schema directly, without historical table-adoption SQL.
            Assert.DoesNotContain(migrations.SelectMany(m => m.UpOperations), operation => operation is SqlOperation);
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
