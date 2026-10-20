using Devkit.Identity.Infrastructure.Configuration;
using Devkit.Identity.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Devkit.Identity.Infrastructure.Identity;

internal sealed class IdentityBootstrapHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<BootstrapAccountOptions> bootstrapOptions,
    ILogger<IdentityBootstrapHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var options = bootstrapOptions.Value;
        var administratorCreated = await IdentityDataSeeder.SeedAsync(dbContext, options, cancellationToken);
        if (administratorCreated)
        {
            logger.LogInformation("Bootstrap administrator {UserName} was created", options.UserName.Trim());
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
