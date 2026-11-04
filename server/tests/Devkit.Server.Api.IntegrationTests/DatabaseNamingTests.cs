using System.Text.RegularExpressions;
using Devkit.Business.Infrastructure.Persistence;
using Devkit.FileStorage.Infrastructure.Persistence;
using Devkit.Application.Infrastructure.Persistence;
using Devkit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Devkit.Server.Api.IntegrationTests;

public sealed class DatabaseNamingTests
{
    [Theory]
    [InlineData("UserId", "user_id")]
    [InlineData("CreatedAtUtc", "created_at_utc")]
    [InlineData("IPAddress", "ip_address")]
    [InlineData("Sha256", "sha256")]
    [InlineData("SHA256Hash", "sha256_hash")]
    [InlineData("already_snake_case", "already_snake_case")]
    public void Word_boundaries_and_acronyms_use_invariant_snake_case(string original, string expected)
        => Assert.Equal(expected, DatabaseNamingExtensions.ToSnakeCase(original));

    [Theory]
    [InlineData("identity", "sys_user sys_role sys_user_role sys_user_claim sys_role_claim sys_user_login sys_user_token sys_refresh_token sys_menu sys_menu_state sys_department sys_position sys_occupancy sys_administration_event")]
    [InlineData("files", "file_stored_file file_reference file_migration_item file_storage_location file_storage_job")]
    [InlineData("business", "module_instance module_buffered_command module_command_sequence study_knowledge_base study_source study_source_version study_chunk study_project study_question study_attempt study_mistake study_query_history workspace_job workspace_file_release_outbox")]
    public void Physical_names_and_migrations_match_the_module_contract(string module, string expectedTables)
    {
        using var context = CreateContext(module);
        var model = context.GetService<IDesignTimeModel>().Model;
        var tables = model.GetRelationalModel().Tables.ToArray();
        Assert.Equal(expectedTables.Split(' ').Order(), tables.Select(t => t.Name).Order());
        Assert.False(context.Database.HasPendingModelChanges());

        var migrator = context.GetService<IMigrator>();
        var script = migrator.GenerateScript();
        var idempotent = migrator.GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("__EFMigrationsHistory", idempotent);
        Assert.Contains("[MigrationId]", idempotent);

        var constraintNames = new List<string>();
        foreach (var table in tables)
        {
            AssertSnakeCase(table.Name);
            Assert.Contains($"CREATE TABLE [{table.Name}]", script);
            Assert.Contains($"CREATE TABLE [{table.Name}]", idempotent);
            foreach (var column in table.Columns)
            {
                AssertSnakeCase(column.Name);
                Assert.Contains($"[{column.Name}] {column.StoreType}", script);
            }
            foreach (var key in table.UniqueConstraints)
            {
                AssertSnakeCase(key.Name);
                Assert.Contains($"CONSTRAINT [{key.Name}]", script);
                constraintNames.Add(key.Name);
            }
            foreach (var foreignKey in table.ForeignKeyConstraints)
            {
                AssertSnakeCase(foreignKey.Name);
                Assert.Contains($"CONSTRAINT [{foreignKey.Name}]", script);
                Assert.Contains($"REFERENCES [{foreignKey.PrincipalTable.Name}]", script);
                constraintNames.Add(foreignKey.Name);
            }
            foreach (var index in table.Indexes)
            {
                AssertSnakeCase(index.Name);
                Assert.Contains($"INDEX [{index.Name}]", script);
                if (index.Filter is { } filter)
                {
                    foreach (Match identifier in Regex.Matches(filter, @"\[([^\]]+)\]"))
                        AssertSnakeCase(identifier.Groups[1].Value);
                    Assert.Contains($"WHERE {filter}", script);
                }
            }
            Assert.Equal(table.Indexes.Count(), table.Indexes.Select(i => i.Name).Distinct().Count());
        }
        Assert.Equal(constraintNames.Count, constraintNames.Distinct().Count());

        // Mapping must not rename the CLR/shadow properties used by repositories and concurrency checks.
        foreach (var entity in model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
                Assert.Equal(DatabaseNamingExtensions.ToSnakeCase(property.Name), property.GetColumnName());

        if (module == "identity")
        {
            Assert.Contains("INSERT INTO [sys_menu_state] ([id], [version])", script);
            Assert.Contains("VALUES (1, 0)", script);
        }
        if (module == "files")
        {
            Assert.Contains("[is_deleted] = 0", script);
            Assert.Contains("[writable] = 1 AND [is_deleted] = 0", script);
            Assert.Contains("[module] nvarchar(450) NOT NULL", script);
        }
    }

    private static void AssertSnakeCase(string name)
        => Assert.Matches("^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$", name);

    private static DbContext CreateContext(string module) => module switch
    {
        "identity" => new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer().Options),
        "files" => new FileStorageDbContext(new DbContextOptionsBuilder<FileStorageDbContext>().UseSqlServer().Options),
        "business" => new BusinessDbContext(new DbContextOptionsBuilder<BusinessDbContext>().UseSqlServer().Options),
        _ => throw new ArgumentOutOfRangeException(nameof(module))
    };
}
