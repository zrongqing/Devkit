using Devkit.Infrastructure.Persistence;
using Devkit.Identity.Infrastructure.Configuration;
using Devkit.Identity.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Devkit.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var connectionString = DatabaseConfiguration.GetDesignTimeConnectionString(args);
        var bootstrapAccount = new BootstrapAccountOptions
        {
            Enabled = bool.TryParse(
                Environment.GetEnvironmentVariable("BootstrapAccount__Enabled"),
                out var enabled) && enabled,
            UserName = Environment.GetEnvironmentVariable("BootstrapAccount__UserName") ?? "admin",
            Email = Environment.GetEnvironmentVariable("BootstrapAccount__Email") ?? "admin@example.invalid",
            Password = Environment.GetEnvironmentVariable("BootstrapAccount__Password") ?? "admin"
        };
        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer(connectionString, sqlServer => sqlServer.EnableRetryOnFailure());
        if (bootstrapAccount.Enabled)
            optionsBuilder.UseIdentityDataSeeding(bootstrapAccount);
        return new IdentityDbContext(optionsBuilder.Options);
    }
}
