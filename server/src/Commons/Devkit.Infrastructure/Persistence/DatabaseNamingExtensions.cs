using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Devkit.Infrastructure.Persistence;

public static class DatabaseNamingExtensions
{
    public static string ToSnakeCase(string name)
    {
        var words = Regex.Replace(name, "([A-Z]+)([A-Z][a-z])", "$1_$2");
        return Regex.Replace(words, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
    }

    // Table names are explicit module-owned mappings; CLR and shadow property names stay intact.
    public static ModelBuilder UseSnakeCaseDatabaseNames(this ModelBuilder builder)
    {
        foreach (var entity in builder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
                property.SetColumnName(ToSnakeCase(property.GetColumnName()));
            foreach (var key in entity.GetKeys())
                key.SetName(ToSnakeCase(key.GetName()!));
            foreach (var index in entity.GetIndexes())
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName()!));
        }

        foreach (var entity in builder.Model.GetEntityTypes())
            foreach (var foreignKey in entity.GetForeignKeys())
                foreignKey.SetConstraintName(ToSnakeCase(foreignKey.GetConstraintName()!));

        return builder;
    }
}
