using Devkit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Devkit.FileStorage.Infrastructure.Persistence;

public sealed class FileStorageDbContextFactory : IDesignTimeDbContextFactory<FileStorageDbContext>
{
    public FileStorageDbContext CreateDbContext(string[] args)
    {
        var connectionString = DatabaseConfiguration.GetDesignTimeConnectionString(args);
        var optionsBuilder = new DbContextOptionsBuilder<FileStorageDbContext>()
            .UseSqlServer(connectionString, sqlServer => sqlServer.EnableRetryOnFailure());
        return new FileStorageDbContext(optionsBuilder.Options);
    }
}
