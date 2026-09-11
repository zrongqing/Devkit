using Devkit.Server.Infrastructure.Configuration;
using Devkit.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Devkit.Server.Infrastructure.Workers;

internal sealed class RefreshTokenCleanupWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<RefreshTokenCleanupOptions> options,
    TimeProvider timeProvider,
    ILogger<RefreshTokenCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Refresh-token cleanup worker is disabled");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.Value.IntervalMinutes), timeProvider);
        do
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Refresh-token cleanup cycle failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DevkitDbContext>();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expired = await dbContext.RefreshTokens
            .Where(token => token.ExpiresAtUtc <= now)
            .ToListAsync(cancellationToken);
        foreach (var token in expired)
        {
            token.IsDeleted = true;
        }

        if (expired.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Soft-deleted {RefreshTokenCount} expired refresh tokens", expired.Count);
        }
    }
}
