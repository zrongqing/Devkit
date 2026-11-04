using Devkit.Infrastructure.Persistence;
using Devkit.Application.Infrastructure.Configuration;
using Devkit.Application.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Devkit.Application.Infrastructure.Persistence;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
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
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString, sqlServer => sqlServer.EnableRetryOnFailure());
        if (bootstrapAccount.Enabled)
            optionsBuilder.UseIdentityDataSeeding(bootstrapAccount);
        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
