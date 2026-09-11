using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Devkit.Server.Infrastructure.Persistence;

public sealed class DevkitDbContextFactory : IDesignTimeDbContextFactory<DevkitDbContext>
{
    public DevkitDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Server=localhost;Database=Devkit;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<DevkitDbContext>()
            .UseSqlServer(connectionString, sqlServer => sqlServer.EnableRetryOnFailure())
            .Options;
        return new DevkitDbContext(options);
    }
}
