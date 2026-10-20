using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Devkit.FileStorage.Infrastructure.Persistence;

public sealed class FileStorageDbContextFactory : IDesignTimeDbContextFactory<FileStorageDbContext>
{
    public FileStorageDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__FileStorage")
            ?? "Server=localhost;Database=DevkitFileStorage;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True";
        var optionsBuilder = new DbContextOptionsBuilder<FileStorageDbContext>()
            .UseSqlServer(connectionString, sqlServer => sqlServer.EnableRetryOnFailure());
        return new FileStorageDbContext(optionsBuilder.Options);
    }
}
