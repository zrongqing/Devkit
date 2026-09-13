using Devkit.Server.Infrastructure.Configuration;
using Devkit.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Devkit.Server.Infrastructure.Identity;

internal static class IdentityDataSeedingExtensions
{
    public static DbContextOptionsBuilder UseIdentityDataSeeding(
        this DbContextOptionsBuilder optionsBuilder,
        BootstrapAccountOptions bootstrapAccount)
    {
        return optionsBuilder
            .UseSeeding((context, _) =>
                IdentityDataSeeder.Seed((DevkitDbContext)context, bootstrapAccount))
            .UseAsyncSeeding((context, _, cancellationToken) =>
                IdentityDataSeeder.SeedAsync(
                    (DevkitDbContext)context,
                    bootstrapAccount,
                    cancellationToken));
    }
}
