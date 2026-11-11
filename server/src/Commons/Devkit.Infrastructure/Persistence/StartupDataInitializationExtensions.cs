using Devkit.DomainCommons.Initialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Devkit.Infrastructure.Persistence;

public static class StartupDataInitializationExtensions
{
    /// <summary>Runs the initializer once per host start, in its own background service and DI scope.</summary>
    public static IServiceCollection AddStartupDataInitializer<TInitializer>(this IServiceCollection services)
        where TInitializer : class, IStartupDataInitializer
    {
        services.TryAddScoped<TInitializer>();
        return services.AddHostedService<StartupDataWorker<TInitializer>>();
    }

    private sealed class StartupDataWorker<TInitializer>(
        IServiceScopeFactory scopes,
        ILogger<StartupDataWorker<TInitializer>> logger) : BackgroundService
        where TInitializer : class, IStartupDataInitializer
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var initializer = scope.ServiceProvider.GetRequiredService<TInitializer>();
                await initializer.InitializeAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Startup data initialization failed for {Initializer}", typeof(TInitializer).FullName);
                throw;
            }
        }
    }
}
