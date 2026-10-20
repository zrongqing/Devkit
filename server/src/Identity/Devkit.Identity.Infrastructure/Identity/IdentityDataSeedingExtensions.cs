using Devkit.Identity.Infrastructure.Configuration;
using Devkit.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Devkit.Identity.Infrastructure.Identity;

internal static class IdentityDataSeedingExtensions
{
    public static DbContextOptionsBuilder UseIdentityDataSeeding(
        this DbContextOptionsBuilder optionsBuilder,
        BootstrapAccountOptions bootstrapAccount)
    {
        return optionsBuilder
            .UseSeeding((context, _) =>
                IdentityDataSeeder.Seed((IdentityDbContext)context, bootstrapAccount))
            .UseAsyncSeeding((context, _, cancellationToken) =>
                IdentityDataSeeder.SeedAsync(
                    (IdentityDbContext)context,
                    bootstrapAccount,
                    cancellationToken));
    }
}
