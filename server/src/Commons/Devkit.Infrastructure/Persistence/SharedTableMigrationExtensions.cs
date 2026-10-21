using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Devkit.Infrastructure.Persistence;

public static class SharedTableMigrationExtensions
{
    // Only initial table adoption uses this. Later schema changes remain explicit migrations.
    public static void AdoptExistingSqlServerTables(this MigrationBuilder migrationBuilder)
    {
        using var context = new DbContext(new DbContextOptionsBuilder().UseSqlServer().Options);
        var generator = context.GetService<IMigrationsSqlGenerator>();
        for (var index = 0; index < migrationBuilder.Operations.Count; index++)
        {
            var operation = migrationBuilder.Operations[index];
            var condition = operation switch
            {
                CreateTableOperation table => $"OBJECT_ID(N'{ObjectName(table.Schema, table.Name)}', N'U') IS NULL",
                CreateIndexOperation createIndex => $"NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'{ObjectName(createIndex.Schema, createIndex.Table)}') AND name = N'{Literal(createIndex.Name)}')",
                _ => null
            };
            if (condition is null) continue;
            var sql = string.Join(Environment.NewLine, generator.Generate([operation]).Select(command => command.CommandText));
            migrationBuilder.Operations[index] = new SqlOperation { Sql = $"IF {condition}\nBEGIN\n{sql}\nEND;" };
        }
    }

    private static string ObjectName(string? schema, string table) => $"[{Literal((schema ?? "dbo").Replace("]", "]]"))}].[{Literal(table.Replace("]", "]]"))}]";
    private static string Literal(string value) => value.Replace("'", "''");
}
