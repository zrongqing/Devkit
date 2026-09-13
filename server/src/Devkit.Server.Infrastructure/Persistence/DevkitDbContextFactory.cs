using Devkit.Server.Infrastructure.Configuration;
using Devkit.Server.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Devkit.Server.Infrastructure.Persistence;

public sealed class DevkitDbContextFactory : IDesignTimeDbContextFactory<DevkitDbContext>
{
    public DevkitDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Server=localhost;Database=Devkit;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True";
        var bootstrapAccount = new BootstrapAccountOptions
        {
            Enabled = bool.TryParse(
                Environment.GetEnvironmentVariable("BootstrapAccount__Enabled"),
                out var enabled) && enabled,
            UserName = Environment.GetEnvironmentVariable("BootstrapAccount__UserName") ?? string.Empty,
            Email = Environment.GetEnvironmentVariable("BootstrapAccount__Email") ?? string.Empty,
            Password = Environment.GetEnvironmentVariable("BootstrapAccount__Password") ?? string.Empty
        };
        var optionsBuilder = new DbContextOptionsBuilder<DevkitDbContext>()
            .UseSqlServer(connectionString, sqlServer => sqlServer.EnableRetryOnFailure());
        optionsBuilder.UseIdentityDataSeeding(bootstrapAccount);
        return new DevkitDbContext(optionsBuilder.Options);
    }
}
