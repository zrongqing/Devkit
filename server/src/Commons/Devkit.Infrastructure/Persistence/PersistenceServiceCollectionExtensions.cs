using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Devkit.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddDevkitSqlServer<TContext>(this IServiceCollection services,
        IConfiguration configuration, string connectionName, Action<DbContextOptionsBuilder>? configure = null)
        where TContext : DbContext
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);
        services.AddHttpContextAccessor();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<AuditingSaveChangesInterceptor>();
        services.AddDbContext<TContext>((provider, options) =>
        {
            var connectionString = configuration.GetConnectionString(connectionName)
                ?? throw new InvalidOperationException($"ConnectionStrings:{connectionName} is required");
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
            configure?.Invoke(options);
            options.AddInterceptors(provider.GetRequiredService<AuditingSaveChangesInterceptor>());
        });
        services.AddHealthChecks().AddCheck<SqlServerHealthCheck<TContext>>("sql-server", tags: ["ready"]);
        return services;
    }
}
