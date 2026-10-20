using Devkit.Business.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Devkit.Business.Infrastructure.Persistence;

public sealed class BusinessDbContextFactory : IDesignTimeDbContextFactory<BusinessDbContext>
{
    public BusinessDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Business")
            ?? "Server=localhost;Database=DevkitBusiness;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True";
        var optionsBuilder = new DbContextOptionsBuilder<BusinessDbContext>()
            .UseSqlServer(connectionString, sqlServer => sqlServer.EnableRetryOnFailure());
        return new BusinessDbContext(optionsBuilder.Options);
    }
}
