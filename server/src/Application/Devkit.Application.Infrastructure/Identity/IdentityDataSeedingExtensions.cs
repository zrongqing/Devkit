using Devkit.Application.Infrastructure.Configuration;
using Devkit.Application.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Devkit.Application.Infrastructure.Identity;

internal static class IdentityDataSeedingExtensions
{
    public static DbContextOptionsBuilder UseIdentityDataSeeding(
        this DbContextOptionsBuilder optionsBuilder,
        BootstrapAccountOptions bootstrapAccount)
    {
        return optionsBuilder
            .UseSeeding((context, _) =>
                IdentityDataSeeder.Seed((ApplicationDbContext)context, bootstrapAccount))
            .UseAsyncSeeding((context, _, cancellationToken) =>
                IdentityDataSeeder.SeedAsync(
                    (ApplicationDbContext)context,
                    bootstrapAccount,
                    cancellationToken));
    }
}
